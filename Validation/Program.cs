using OpenCvSharp;
using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Infrastructure.Services;
using System;
using System.Text;

namespace PlantConditionAnalyzer.Validation
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;

            // Adathalmaz mappa struktúra:
            // Dataset/
            //   ├── Images/       (eredeti képek)
            //   └── GroundTruth/  (B&W referencia maszkok, ugyanolyan névvel)


            string datasetPath = @"C:\Users\balin\Desktop\Egyetem\SZPT\tesztkepek\Dataset";
            string imagesFolder = Path.Combine(datasetPath, "Images");
            string groundTruthFolder = Path.Combine(datasetPath, "GroundTruth");
            string outputFolder = Path.Combine(datasetPath, "Results");

            Directory.CreateDirectory(outputFolder);

            var validator = new SegmentationValidator(imagesFolder, groundTruthFolder, outputFolder);

            Console.WriteLine("Validating current segmentation method (ExG + LAB)...\n");
            await validator.ValidateSegmentationAsync();

            Console.WriteLine("\n✓ Validation complete! Check the Results folder.");
            Console.WriteLine($"  Location: {outputFolder}");

        }
    }




    public class SegmentationValidator
    {
        private readonly string imagesFolder;
        private readonly string groundTruthFolder;
        private readonly string outputFolder;

        public SegmentationValidator(string imagesFolder, string groundTruthFolder, string outputFolder)
        {
            this.imagesFolder = imagesFolder;
            this.groundTruthFolder = groundTruthFolder;
            this.outputFolder = outputFolder;
        }

        public async Task ValidateSegmentationAsync()
        {
            var imageFiles = Directory.GetFiles(imagesFolder, "*.jpg")
                .Concat(Directory.GetFiles(imagesFolder, "*.png"))
                .Concat(Directory.GetFiles(imagesFolder, "*.bmp"))
                .ToArray();

            if (imageFiles.Length == 0)
            {
                Console.WriteLine("❌ No images found in Images folder!");
                return;
            }

            Console.WriteLine($"Found {imageFiles.Length} images to process.\n");

            var results = new List<ValidationResult>();

            foreach (var imagePath in imageFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(imagePath);
                string extension = Path.GetExtension(imagePath);

                // Próbáljuk meg különböző kiterjesztésekkel is
                string gtPath = Path.Combine(groundTruthFolder, fileName + ".png");
                if (!File.Exists(gtPath))
                    gtPath = Path.Combine(groundTruthFolder, fileName + ".jpg");
                if (!File.Exists(gtPath))
                    gtPath = Path.Combine(groundTruthFolder, fileName + ".bmp");

                if (!File.Exists(gtPath))
                {
                    Console.WriteLine($"⚠️  Ground truth missing for: {fileName}");
                    continue;
                }

                Console.Write($"Processing: {fileName}... ");

                try
                {
                    using Mat original = Cv2.ImRead(imagePath, ImreadModes.Color);
                    using Mat groundTruth = Cv2.ImRead(gtPath, ImreadModes.Grayscale);

                    if (original.Empty() || groundTruth.Empty())
                    {
                        Console.WriteLine("❌ Failed to load image");
                        continue;
                    }

                    using Mat predicted = GetSegmentationMask(original);

                    var result = CalculateMetrics(groundTruth, predicted, fileName);
                    results.Add(result);

                    SaveVisualization(imagePath, gtPath, predicted, result);

                    Console.WriteLine($"✓ IoU: {result.IoU:F3} | F1: {result.F1Score:F3}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }

            if (results.Count > 0)
            {
                GenerateReport(results);
            }
            else
            {
                Console.WriteLine("\n❌ No results to report.");
            }
        }

        private Mat GetSegmentationMask(Mat original)
        {
            using Mat preMask = new Mat();
            using Mat blurred = new Mat();
            Cv2.GaussianBlur(original, blurred, new Size(5, 5), 0);

            // 1. ExG alapú előszegmentálás
            using (Mat exg = CalculateExG(blurred))
            using (Mat exg8 = new Mat())
            {
                Cv2.Normalize(exg, exg8, 0, 255, NormTypes.MinMax, MatType.CV_8U);
                Cv2.Threshold(exg8, preMask, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
            }

            if (IsMaskInverted(preMask))
                Cv2.BitwiseNot(preMask, preMask);

            FilterSmallBlobs(preMask, 200);

            int totalPixels = preMask.Rows * preMask.Cols;
            int plantPixels = preMask.CountNonZero();

            // Ha túl kicsi vagy túl nagy a lefedettség, ne finomítsunk
            if ((plantPixels < (totalPixels * 0.01)) || (plantPixels > (totalPixels * 0.99)))
            {
                return preMask.Clone();
            }

            // 2. LAB színtér alapú finomítás
            using Mat labImage = new();
            Cv2.CvtColor(original, labImage, ColorConversionCodes.BGR2Lab);
            Cv2.Split(labImage, out Mat[] labChannels);
            using Mat channelA = labChannels[1];
            labChannels[0].Dispose();
            labChannels[2].Dispose();

            using Mat bgMask = new();
            Cv2.BitwiseNot(preMask, bgMask);

            Cv2.MeanStdDev(channelA, out Scalar meanPlant, out Scalar stdPlant, preMask);
            Cv2.MeanStdDev(channelA, out Scalar meanBg, out Scalar stdBg, bgMask);

            double u_plant = meanPlant.Val0;
            double u_bg = meanBg.Val0;

            // Ha túl közel vannak egymáshoz, maradjunk az ExG-nél
            if (Math.Abs(u_plant - u_bg) < 5.0)
            {
                return preMask.Clone();
            }

            // Küszöb számítás
            double thresholdValue = ((meanPlant.Val0 + stdPlant.Val0) + (meanBg.Val0 - stdBg.Val0)) / 2.0;
            if (u_plant > u_bg)
                thresholdValue = ((meanPlant.Val0 - stdPlant.Val0) + (meanBg.Val0 + stdBg.Val0)) / 2.0;

            Mat finalMask = new Mat();
            var type = u_plant < u_bg ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
            Cv2.Threshold(channelA, finalMask, thresholdValue, 255, type);

            FilterSmallBlobs(finalMask, 300);
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(finalMask, finalMask, MorphTypes.Close, kernel);

            return finalMask;
        }

        private Mat CalculateExG(Mat blurred)
        {
            Cv2.Split(blurred, out Mat[] channels);
            using Mat b = new Mat();
            using Mat g = new Mat();
            using Mat r = new Mat();

            channels[0].ConvertTo(b, MatType.CV_32F);
            channels[1].ConvertTo(g, MatType.CV_32F);
            channels[2].ConvertTo(r, MatType.CV_32F);

            foreach (var c in channels) c.Dispose();

            // ExG = 2*G - R - B
            Mat result = new Mat();
            using (Mat g2 = g * 2)
            using (Mat diff = g2 - r)
                Cv2.Subtract(diff, b, result);

            return result;
        }

        private bool IsMaskInverted(Mat mask)
        {
            int isInvertedCorner = 0;
            if (mask.At<byte>(0, 0) > 0) isInvertedCorner++;
            if (mask.At<byte>(0, mask.Cols - 1) > 0) isInvertedCorner++;
            if (mask.At<byte>(mask.Rows - 1, 0) > 0) isInvertedCorner++;
            if (mask.At<byte>(mask.Rows - 1, mask.Cols - 1) > 0) isInvertedCorner++;
            return isInvertedCorner >= 3;
        }

        private void FilterSmallBlobs(Mat mask, double minArea)
        {
            Cv2.FindContours(mask, out Point[][] contours, out HierarchyIndex[] hierarchy,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                double area = Cv2.ContourArea(contour);
                if (area < minArea)
                {
                    Cv2.DrawContours(mask, new[] { contour }, -1, Scalar.Black, -1);
                }
            }
        }

        private ValidationResult CalculateMetrics(Mat groundTruth, Mat predicted, string imageName)
        {
            // Bináris maszkok biztosítása
            using Mat gtBinary = new Mat();
            using Mat predBinary = new Mat();
            Cv2.Threshold(groundTruth, gtBinary, 127, 255, ThresholdTypes.Binary);
            Cv2.Threshold(predicted, predBinary, 127, 255, ThresholdTypes.Binary);

            // True Positive: GT=1 ÉS Pred=1
            using Mat tp = new Mat();
            Cv2.BitwiseAnd(gtBinary, predBinary, tp);
            int tpCount = Cv2.CountNonZero(tp);

            // False Positive: GT=0 ÉS Pred=1
            using Mat gtInv = new Mat();
            Cv2.BitwiseNot(gtBinary, gtInv);
            using Mat fp = new Mat();
            Cv2.BitwiseAnd(gtInv, predBinary, fp);
            int fpCount = Cv2.CountNonZero(fp);

            // False Negative: GT=1 ÉS Pred=0
            using Mat predInv = new Mat();
            Cv2.BitwiseNot(predBinary, predInv);
            using Mat fn = new Mat();
            Cv2.BitwiseAnd(gtBinary, predInv, fn);
            int fnCount = Cv2.CountNonZero(fn);

            // True Negative: GT=0 ÉS Pred=0
            using Mat tn = new Mat();
            Cv2.BitwiseAnd(gtInv, predInv, tn);
            int tnCount = Cv2.CountNonZero(tn);

            // Metrikák számítása
            double precision = tpCount > 0 ? tpCount / (double)(tpCount + fpCount) : 0;
            double recall = tpCount > 0 ? tpCount / (double)(tpCount + fnCount) : 0;
            double f1Score = (precision + recall) > 0 ? 2 * (precision * recall) / (precision + recall) : 0;
            double accuracy = (tpCount + tnCount) / (double)(tpCount + fpCount + fnCount + tnCount);
            double iou = tpCount > 0 ? tpCount / (double)(tpCount + fpCount + fnCount) : 0;

            return new ValidationResult
            {
                ImageName = imageName,
                TruePositive = tpCount,
                FalsePositive = fpCount,
                FalseNegative = fnCount,
                TrueNegative = tnCount,
                Precision = precision,
                Recall = recall,
                F1Score = f1Score,
                Accuracy = accuracy,
                IoU = iou
            };
        }

        private void SaveVisualization(string imagePath, string gtPath, Mat predicted, ValidationResult result)
        {
            using Mat original = Cv2.ImRead(imagePath);
            using Mat gt = Cv2.ImRead(gtPath, ImreadModes.Grayscale);

            // 2x2 grid layout
            int w = original.Width;
            int h = original.Height;

            using Mat canvas = new Mat(h * 2, w * 2, MatType.CV_8UC3, new Scalar(240, 240, 240));

            // 1. Original (bal felső)
            original.CopyTo(canvas[new Rect(0, 0, w, h)]);
            Cv2.PutText(canvas, "Original", new Point(10, 30),
                HersheyFonts.HersheySimplex, 1.0, new Scalar(255, 255, 255), 2);

            // 2. Ground Truth (jobb felső)
            using Mat gtColor = new Mat();
            Cv2.CvtColor(gt, gtColor, ColorConversionCodes.GRAY2BGR);
            gtColor.CopyTo(canvas[new Rect(w, 0, w, h)]);
            Cv2.PutText(canvas, "Ground Truth", new Point(w + 10, 30),
                HersheyFonts.HersheySimplex, 1.0, new Scalar(255, 255, 255), 2);

            // 3. Predicted (bal alsó)
            using Mat predColor = new Mat();
            Cv2.CvtColor(predicted, predColor, ColorConversionCodes.GRAY2BGR);
            predColor.CopyTo(canvas[new Rect(0, h, w, h)]);
            Cv2.PutText(canvas, "Predicted", new Point(10, h + 30),
                HersheyFonts.HersheySimplex, 1.0, new Scalar(255, 255, 255), 2);

            // 4. Overlay comparison (jobb alsó)
            using Mat overlay = original.Clone();
            using Mat gtBinary = new Mat();
            using Mat predBinary = new Mat();
            Cv2.Threshold(gt, gtBinary, 127, 255, ThresholdTypes.Binary);
            Cv2.Threshold(predicted, predBinary, 127, 255, ThresholdTypes.Binary);

            // TP = Zöld, FP = Piros, FN = Kék
            using Mat tp = new Mat();
            Cv2.BitwiseAnd(gtBinary, predBinary, tp);
            overlay.SetTo(new Scalar(0, 255, 0), tp); // Zöld

            using Mat gtInv = new Mat();
            Cv2.BitwiseNot(gtBinary, gtInv);
            using Mat fp = new Mat();
            Cv2.BitwiseAnd(gtInv, predBinary, fp);
            overlay.SetTo(new Scalar(0, 0, 255), fp); // Piros

            using Mat predInv = new Mat();
            Cv2.BitwiseNot(predBinary, predInv);
            using Mat fn = new Mat();
            Cv2.BitwiseAnd(gtBinary, predInv, fn);
            overlay.SetTo(new Scalar(255, 0, 0), fn); // Kék

            overlay.CopyTo(canvas[new Rect(w, h, w, h)]);

            // Legend
            Cv2.PutText(canvas, "Overlay", new Point(w + 10, h + 30),
                HersheyFonts.HersheySimplex, 1.0, new Scalar(255, 255, 255), 2);
            Cv2.PutText(canvas, "Green=TP Red=FP Blue=FN", new Point(w + 10, h + 60),
                HersheyFonts.HersheySimplex, 0.6, new Scalar(255, 255, 255), 1);

            // Metrics overlay
            int textY = h * 2 - 120;
            Cv2.Rectangle(canvas, new Point(10, textY - 10), new Point(500, h * 2 - 10),
                new Scalar(0, 0, 0), -1);

            string[] metrics = new[]
            {
                $"IoU: {result.IoU:F4}",
                $"F1-Score: {result.F1Score:F4}",
                $"Precision: {result.Precision:F4}",
                $"Recall: {result.Recall:F4}"
            };

            for (int i = 0; i < metrics.Length; i++)
            {
                Cv2.PutText(canvas, metrics[i], new Point(20, textY + 20 + i * 25),
                    HersheyFonts.HersheySimplex, 0.7, new Scalar(255, 255, 255), 2);
            }

            string fileName = Path.GetFileNameWithoutExtension(result.ImageName);
            string outputPath = Path.Combine(outputFolder, $"{fileName}_validation.png");
            Cv2.ImWrite(outputPath, canvas);
        }

        private void GenerateReport(List<ValidationResult> results)
        {
            double avgPrecision = results.Average(r => r.Precision);
            double avgRecall = results.Average(r => r.Recall);
            double avgF1 = results.Average(r => r.F1Score);
            double avgAccuracy = results.Average(r => r.Accuracy);
            double avgIoU = results.Average(r => r.IoU);

            Console.WriteLine("\n" + new string('=', 70));
            Console.WriteLine("SEGMENTATION VALIDATION SUMMARY (ExG + LAB)");
            Console.WriteLine(new string('=', 70));
            Console.WriteLine($"Images processed: {results.Count}");
            Console.WriteLine($"Average Precision: {avgPrecision:F4} ({avgPrecision * 100:F2}%)");
            Console.WriteLine($"Average Recall:    {avgRecall:F4} ({avgRecall * 100:F2}%)");
            Console.WriteLine($"Average F1-Score:  {avgF1:F4}");
            Console.WriteLine($"Average Accuracy:  {avgAccuracy:F4} ({avgAccuracy * 100:F2}%)");
            Console.WriteLine($"Average IoU:       {avgIoU:F4} ({avgIoU * 100:F2}%)");
            Console.WriteLine(new string('=', 70));

            // CSV export
            string csvPath = Path.Combine(outputFolder, "validation_report.csv");
            using var writer = new StreamWriter(csvPath);

            writer.WriteLine("Image,TP,FP,FN,TN,Precision,Recall,F1,Accuracy,IoU");
            foreach (var r in results)
            {
                writer.WriteLine($"{r.ImageName},{r.TruePositive},{r.FalsePositive}," +
                    $"{r.FalseNegative},{r.TrueNegative},{r.Precision:F4},{r.Recall:F4}," +
                    $"{r.F1Score:F4},{r.Accuracy:F4},{r.IoU:F4}");
            }

            writer.WriteLine();
            writer.WriteLine($"AVERAGE,,,,,{avgPrecision:F4},{avgRecall:F4}," +
                $"{avgF1:F4},{avgAccuracy:F4},{avgIoU:F4}");

            Console.WriteLine($"\n✓ CSV report saved: {csvPath}");
        }
    }


    public class ValidationResult
    {
        public string ImageName { get; set; } = "";
        public int TruePositive { get; set; }
        public int FalsePositive { get; set; }
        public int FalseNegative { get; set; }
        public int TrueNegative { get; set; }
        public double Precision { get; set; }
        public double Recall { get; set; }
        public double F1Score { get; set; }
        public double Accuracy { get; set; }
        public double IoU { get; set; }
    }
}
