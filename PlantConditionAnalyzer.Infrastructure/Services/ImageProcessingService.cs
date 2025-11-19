using OpenCvSharp;
using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Infrastructure.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        public ProcessingResult ProcessImage(string imagePath, VegetationIndex indexType=VegetationIndex.ExG)
        {
            using Mat rawOriginal = Cv2.ImRead(imagePath, ImreadModes.Color);

            if (rawOriginal.Empty())
            {
                throw new Exception($"Cannot read image file: {imagePath}");
            }
            return ProcessImage(rawOriginal, indexType);
        }
        public ProcessingResult ProcessImage(Mat rawOriginal, VegetationIndex indexType = VegetationIndex.ExG)
        {
           
            using Mat original = ApplyROI(rawOriginal, 0.05);

            using Mat plantMask=GetSegmentationMask(original);
            using Mat rawIndexMap = CalculateIndexImage(original, indexType);
            using Mat normalizedIndexMap = new();
            Cv2.Normalize(rawIndexMap, normalizedIndexMap, 0, 255, NormTypes.MinMax, MatType.CV_8U);
            



           

            using Mat heatmap = new Mat();
            Cv2.ApplyColorMap(normalizedIndexMap, heatmap, ColormapTypes.Turbo);

            // Maszkolás: Csak a növény maradjon színes
            using Mat maskedHeatmap = Mat.Zeros(original.Size(), original.Type());
            heatmap.CopyTo(maskedHeatmap, plantMask);

            // Összefésülés az eredetivel
            using Mat finalImage = new Mat();
            Cv2.AddWeighted(original,1.0,maskedHeatmap,0.5,0,finalImage);


            // NINCS HÁTTÉR
            using Mat maskedOriginal = new Mat();
            original.CopyTo(maskedOriginal, plantMask);
            Cv2.AddWeighted(maskedOriginal, 1.0, maskedHeatmap, 0.5, 0, finalImage);

            double plantAreaPercentage = (double)Cv2.CountNonZero(plantMask) / (plantMask.Rows * plantMask.Cols) * 100.0;

            Cv2.MeanStdDev(normalizedIndexMap, out Scalar meanVis, out Scalar stdVis, plantMask);

            var stats = new Snapshot
            {
                Timestamp = DateTime.Now,
                ImagePath = "n/a",
                VegetationIndexName = indexType.ToString(),
                ViMean = meanVis.Val0,
                ViStdDev = stdVis.Val0,
                PlantAreaPercentage = plantAreaPercentage,
                SpadEstimate = null // Ezt majd egy külön szerviz/metódus számolja
            };

            return new ProcessingResult
            {
                ProcessedImageBytes = finalImage.ToBytes(".bmp"),
                Statistics = stats
            };
        }
        public byte[] GenerateColormapLegend()
        {
            // 1. Létrehozunk egy 256 pixel széles, 1 pixel magas szürke gradienst (0..255)
            using Mat gradient = new Mat(1, 256, MatType.CV_8U);
            for (int i = 0; i < 256; i++)
            {
                gradient.Set<byte>(0, i, (byte)i);
            }

            // 2. Felnagyítjuk, hogy látható csík legyen (256x20 pixel)
            using Mat resized = new Mat();
            Cv2.Resize(gradient, resized, new Size(256, 20), 0, 0, InterpolationFlags.Nearest);

            // 3. Rátesszük ugyanazt a TURBO színezést, amit a növényre is
            using Mat colored = new Mat();
            Cv2.ApplyColorMap(resized, colored, ColormapTypes.Turbo);

            return colored.ToBytes(".bmp");
        }
        private Mat CalculateIndexImage(Mat original, VegetationIndex type)
        {
            using Mat b = new Mat();
            using Mat g = new Mat();
            using Mat r = new Mat();

            // Split és Convert egy lépésben nem megy, így szétbontjuk
            Cv2.Split(original, out Mat[] channels);
            channels[0].ConvertTo(b, MatType.CV_32F); // Blue
            channels[1].ConvertTo(g, MatType.CV_32F); // Green
            channels[2].ConvertTo(r, MatType.CV_32F); // Red

            foreach (var c in channels) c.Dispose(); // Takarítás

            // Az eredményt tároló mátrix
            Mat result = new Mat();

            switch (type)
            {
                case VegetationIndex.ExG:
                    // ExG = 2*G - R - B
                    using (Mat g2 = g * 2)
                    using (Mat diff = g2 - r)
                        Cv2.Subtract(diff, b, result);
                    break;

                case VegetationIndex.ExGR:
                    // ExGR = ExG - ExR = (2G-R-B) - (1.4R-B)
                    // ExR = 1.4*R - G (Meyer et al.)
                    using (Mat exg = (g * 2) - r - b)
                    using (Mat exr = (r * 1.4) - g)
                        Cv2.Subtract(exg, exr, result);
                    break;

                case VegetationIndex.VARI:
                    // VARI = (G - R) / (G + R - B)
                    using (Mat num = g - r)
                    using (Mat den1 = g + r)
                    using (Mat den2 = den1 - b)
                    {
                        Cv2.Divide(num, den2, result);//0-val osztast kezeli
                    }
                    break;

                case VegetationIndex.NGRDI:
                    // NGRDI = (G - R) / (G + R)
                    using (Mat num = g - r)
                    using (Mat den = g + r)
                        Cv2.Divide(num, den, result);
                    break;

                case VegetationIndex.GLI:
                    // GLI = (2G - R - B) / (2G + R + B)
                    using (Mat g2 = g * 2)
                    using (Mat num1 = g2 - r)
                    using (Mat num = num1 - b)
                    using (Mat den1 = g2 + r)
                    using (Mat den = den1 + b)
                        Cv2.Divide(num, den, result);
                    break;

                case VegetationIndex.TGI:
                    // TGI = G - 0.39*R - 0.61*B
                    using (Mat rW = r * 0.39)
                    using (Mat bW = b * 0.61)
                    using (Mat sub1 = g - rW)
                        Cv2.Subtract(sub1, bW, result);
                    break;

                default:
                    // Fallback: ExG
                    using (Mat g2 = g * 2)
                    using (Mat diff = g2 - r)
                        Cv2.Subtract(diff, b, result);
                    break;
            }

            return result;

        }
        private Mat GetSegmentationMask(Mat original)
        {
            using Mat preMask = new Mat();
            using Mat blurred = new Mat();
            Cv2.GaussianBlur(original, blurred, new Size(5, 5), 0);
            // 2. Előszegmentálás (ExG)


            using (Mat exg = CalculateIndexImage(blurred, VegetationIndex.ExG))
            using (Mat exg8 = new Mat())
            {
                Cv2.Normalize(exg, exg8, 0, 255, NormTypes.MinMax, MatType.CV_8U);
                Cv2.Threshold(exg8, preMask, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
            }

            if (IsMaskInverted(preMask)) Cv2.BitwiseNot(preMask, preMask);


         
            FilterSmallBlobs(preMask, 200);


            int totalPixels = preMask.Rows * preMask.Cols;
            int plantPixels = preMask.CountNonZero();

           
            if ((plantPixels < (totalPixels * 0.01)) || (plantPixels > (totalPixels * 0.99)))
            {
                 return preMask.Clone();
            }

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
            // Ha túl közel vannak egymáshoz a színek, maradjunk az ExG-nél
            if (Math.Abs(u_plant - u_bg) < 5.0)
            {
                return preMask.Clone();
            }
            // Küszöb számítás
            double thresholdValue = ((meanPlant.Val0 + stdPlant.Val0) + (meanBg.Val0 - stdBg.Val0)) / 2.0;
            if (u_plant > u_bg) thresholdValue = ((meanPlant.Val0 - stdPlant.Val0) + (meanBg.Val0 + stdBg.Val0)) / 2.0;

            Mat finalMask = new Mat();
            var type = u_plant < u_bg ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
            Cv2.Threshold(channelA, finalMask, thresholdValue, 255, type);


            FilterSmallBlobs(finalMask, 300);
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(finalMask, finalMask, MorphTypes.Close, kernel);

            return finalMask;
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
            // 1. Megkeressük az összes összefüggő alakzatot  a maszkon
            Cv2.FindContours(mask, out Point[][] contours, out HierarchyIndex[] hierarchy,
                RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            foreach (var contour in contours)
            {
                // 3. Ha a területe kisebb, mint a limit...
                double area = Cv2.ContourArea(contour);
                if (area < minArea)
                {
                    // ...akkor feketére (0) színezzük a belsejét (eltüntetjük)
                    // thickness: -1 jelenti a kitöltést
                    Cv2.DrawContours(mask, new[] { contour }, -1, Scalar.Black, -1);
                }
            }
        }
        private Mat ApplyROI(Mat original, double marginPercent)
        {
            int marginX = (int)(original.Cols * marginPercent);
            int marginY = (int)(original.Rows * marginPercent);

            // Létrehozunk egy fekete maszkot
            Mat roiMask = Mat.Zeros(original.Size(), MatType.CV_8U);

            // A közepét fehérre festjük (ez a hasznos terület)
            var roiRect = new Rect(
                marginX,
                marginY,
                original.Cols - (2 * marginX),
                original.Rows - (2 * marginY)
            );

            Cv2.Rectangle(roiMask, roiRect, Scalar.White, -1); // -1 = kitöltés

            // Kivágjuk az eredeti képet ezzel a maszkkal
            Mat cropped = new Mat();
            // Ahol a maszk fekete, ott az eredmény is fekete lesz 
            original.CopyTo(cropped, roiMask);

            return cropped;
        }
    }
}
