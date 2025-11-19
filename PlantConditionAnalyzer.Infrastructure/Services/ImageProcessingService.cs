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
        public ProcessingResult ProcessImage(string imagePath, VegetationIndex indexType)
        {
            using Mat original = Cv2.ImRead(imagePath, ImreadModes.Color);

            if (original.Empty())
            {
                throw new Exception($"Cannot read image file: {imagePath}");
            }
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
            Cv2.AddWeighted(original,1.0,heatmap,0.5,0,finalImage);

            // --- 7. STATISZTIKA & KIMENET ---
            double plantAreaPercentage = (double)Cv2.CountNonZero(plantMask) / (plantMask.Rows * plantMask.Cols) * 100.0;

            // Vitalitás statisztika (ExG értékek alapján, a végső maszkkal)
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
            using Mat preMask = new();
            using Mat exg = CalculateIndexImage(original, VegetationIndex.ExG);


            using (Mat exg8 = new Mat())
            {
                Cv2.Normalize(exg, exg8, 0, 255, NormTypes.MinMax, MatType.CV_8U);//otsu miatt 8bitre valt
                Cv2.Threshold(exg8, preMask, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);
            }

            if (IsMaskInverted(preMask)) Cv2.BitwiseNot(preMask, preMask);



            int totalPixels = preMask.Rows * preMask.Cols;
            int plantPixels = preMask.CountNonZero();
            bool hasEnoughData = (plantPixels > (totalPixels * 0.01)) && (plantPixels < (totalPixels * 0.99));//ellenorizzuk, hogy a kep <99% nem noveny/hatter

           
            if ((plantPixels > (totalPixels * 0.01)) && (plantPixels < (totalPixels * 0.99)))
            {
                 return preMask.Clone();
            }

            using Mat labImage = new();
            Cv2.CvtColor(original, labImage, ColorConversionCodes.BGR2Lab);
            Cv2.Split(labImage, out Mat[] labChannels);
            using Mat channelA = labChannels[1];
            using Mat bgMask = new();
            Cv2.BitwiseNot(preMask, bgMask);

            Cv2.MeanStdDev(channelA, out Scalar meanPlant, out Scalar stdPlant, preMask);
            Cv2.MeanStdDev(channelA, out Scalar meanBg, out Scalar stdBg, bgMask);
            foreach (var c in labChannels) c.Dispose(); // A mentve, LAB torolheto

            double u_plant = meanPlant.Val0;
            double u_bg = meanBg.Val0;

            // Küszöb számítás
            double thresholdValue = ((u_plant + stdPlant.Val0) + (u_bg - stdBg.Val0)) / 2.0;
            if (u_plant > u_bg) thresholdValue = ((u_plant - stdPlant.Val0) + (u_bg + stdBg.Val0)) / 2.0;

            Mat finalMask = new Mat();
            var type = u_plant < u_bg ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
            Cv2.Threshold(channelA, finalMask, thresholdValue, 255, type);

            return finalMask;
        }
        private Mat GetExGMask(Mat original)
        {
            Mat mask = new();
            using Mat exgGray = GetExGImage(original);
            Cv2.Threshold(exgGray, mask, 0, 255, ThresholdTypes.Otsu | ThresholdTypes.Binary);//otsu miatt a thresholdot maganak szamolja, majd binarisra
            if (IsMaskInverted(mask))
            {
                Cv2.BitwiseNot(mask, mask); //megforditjuk, hogyha inverznek vettuk novenyzetet
            }
            return mask;
        }
        private Mat GetExGImage(Mat original)
        {
            Cv2.Split(original, out Mat[] channels);
            using var b = channels[0];
            using var g = channels[1];
            using var r = channels[2];

            using Mat b_16s = new();
            using Mat g_16s = new();
            using Mat r_16s = new();
            b.ConvertTo(b_16s, MatType.CV_16S);
            g.ConvertTo(g_16s, MatType.CV_16S);
            r.ConvertTo(r_16s, MatType.CV_16S);

            using Mat g_x2 = g_16s * 2;
            using Mat temp = g_x2 - r_16s;
            using Mat exg = temp - b_16s;

            // Visszaalakítás 8 bitre 
            Mat exg8 = new Mat();
            Cv2.ConvertScaleAbs(exg, exg8);

            return exg8;
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
    }
}
