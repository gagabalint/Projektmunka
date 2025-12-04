using OpenCvSharp;
using OpenCvSharp.Internal;
using PlantConditionAnalyzer.Core.Enums;
using PlantConditionAnalyzer.Core.Interfaces;
using PlantConditionAnalyzer.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace PlantConditionAnalyzer.Infrastructure.Services
{
    public class ImageProcessingService : IImageProcessingService
    {
        public async Task<ProcessingResult> ProcessImageAsync(string imagePath, VegetationIndex indexType = VegetationIndex.ExG)
        {
            return await Task.Run(() =>
              {
                  using Mat rawOriginal = Cv2.ImRead(imagePath, ImreadModes.Color);

                  if (rawOriginal.Empty())
                  {
                      throw new Exception($"Cannot read image file: {imagePath}");
                  }
                  return ProcessMat(rawOriginal, indexType);
              });
        }
        public async Task<ProcessingResult> ProcessImageAsync(Mat frame, VegetationIndex indexType = VegetationIndex.ExG)
        {
            return await Task.Run(() =>
            {
                using Mat frameClone = frame.Clone();

                return ProcessMat(frameClone, indexType);
            });
        }
        private ProcessingResult ProcessMat(Mat rawOriginal, VegetationIndex indexType = VegetationIndex.ExG)
        {
            double toCut = 0.1;
            using Mat original = ApplyROI(rawOriginal, toCut);

            using Mat plantMask = GetSegmentationMask(original);
            using Mat rawIndexMap = CalculateIndexImage(original, indexType);
            Cv2.MinMaxLoc(rawIndexMap, out double actualMin, out double actualMax);

            // normalizálás fixált tartománnyal
            using Mat normalizedIndexMap = new();
            double theoreticalMin, theoreticalMax;

            switch (indexType)
            {
                case VegetationIndex.ExG:
                case VegetationIndex.ExGR:
                case VegetationIndex.TGI:
                    theoreticalMin = -1.0;
                    theoreticalMax = 2.0;
                    break;
                case VegetationIndex.MGRVI:
                case VegetationIndex.VARI:
                case VegetationIndex.NGRDI:
                case VegetationIndex.GLI:
                    theoreticalMin = -1.0;
                    theoreticalMax = 1.0;
                    break;
                default:
                    theoreticalMin = -1.0;
                    theoreticalMax = 2.0;
                    break;
            }

            // Manuális normalizálás [theoreticalMin, theoreticalMax] → [0, 255]
            using Mat shifted = rawIndexMap - new Scalar(theoreticalMin);
            double scale = 255.0 / (theoreticalMax - theoreticalMin);
            shifted.ConvertTo(normalizedIndexMap, MatType.CV_8U, scale);

            using Mat heatmap = new Mat();
            Cv2.ApplyColorMap(normalizedIndexMap, heatmap, ColormapTypes.Turbo);

            // Maszkolás
            using Mat maskedHeatmap = Mat.Zeros(original.Size(), original.Type());
            heatmap.CopyTo(maskedHeatmap, plantMask);

            // Összefésülés
            using Mat maskedOriginal = new Mat();
            original.CopyTo(maskedOriginal, plantMask);
            using Mat finalImage = new Mat();
            Cv2.AddWeighted(maskedOriginal, 1.0, maskedHeatmap, 0.5, 0, finalImage);
            double rows = plantMask.Rows*(1 - 2 * toCut);
            double cols = plantMask.Cols*(1 - 2 * toCut);
        
            double wholeArea = rows*cols;
            // Statisztikák
            double plantAreaPercentage = (double)Cv2.CountNonZero(plantMask) / wholeArea * 100.0;
            Cv2.MeanStdDev(rawIndexMap, out Scalar meanRaw, out Scalar stdRaw, plantMask);

            var stats = new Snapshot
            {
                Timestamp = DateTime.Now,
                ImagePath = "n/a",
                VegetationIndexName = indexType.ToString(),
                ViMean = meanRaw.Val0,
                ViStdDev = stdRaw.Val0,
                PlantAreaPercentage = plantAreaPercentage,
            };

            return new ProcessingResult
            {
                ProcessedImageBytes = finalImage.ToBytes(".bmp"),
                Statistics = stats
            };
            #region teszt

            //  using Mat normalizedIndexMap = new();
            //  Cv2.Normalize(rawIndexMap, normalizedIndexMap, 0, 255, NormTypes.MinMax, MatType.CV_8U);
            //  using Mat heatmap = new Mat();
            //  Cv2.ApplyColorMap(normalizedIndexMap, heatmap, ColormapTypes.Turbo);

            //  // Novenyzet maszkolasa
            //  using Mat maskedHeatmap = Mat.Zeros(original.Size(), original.Type());
            //  heatmap.CopyTo(maskedHeatmap, plantMask);

            //  // Összefésülés
            //  using Mat finalImage = new Mat();
            ////  Cv2.AddWeighted(original, 1.0, maskedHeatmap, 0.5, 0, finalImage);


            //  // NINCS HÁTTÉR -- vagy ez vagy a felette levo
            //  using Mat maskedOriginal = new Mat();
            //  original.CopyTo(maskedOriginal, plantMask);
            //  Cv2.AddWeighted(maskedOriginal, 1.0, maskedHeatmap, 0.5, 0, finalImage);

            //  double plantAreaPercentage = (double)Cv2.CountNonZero(plantMask) / (plantMask.Rows * plantMask.Cols) * 100.0;
            //  //  Cv2.MeanStdDev(normalizedIndexMap, out Scalar meanVis, out Scalar stdVis, plantMask);
            //  Cv2.MeanStdDev(rawIndexMap, out Scalar meanRaw, out Scalar stdRaw, plantMask);


            //  #region újspad

            //  // Feltételezzük:
            //  // 'result' = a VARI indexek float mátrixa (CV_32F)
            //  // 'mask'   = a bináris maszk (CV_8U), ahol 255 a növény




            //  // Átmásoljuk a C++ memóriából a C# tömbbe
            //  //NGRDI.GetArray(out float[] variValues);
            //  //plantMask.GetArray(out byte[] maskValues);

            //  //// 2. Szűrés: Csak a maszk alatti értékeket gyűjtjük ki
            //  //var validPixels = new List<float>();

            //  //// Ez a ciklus végigfut a képen (optimalizálható pointerrel, de így is gyors)
            //  //for (int i = 0; i < variValues.Length; i++)
            //  //{
            //  //    // Ha a maszk szerint ez növény (nem 0)
            //  //    if (maskValues[i] > 0)
            //  //    {
            //  //        validPixels.Add(variValues[i]);
            //  //    }
            //  //}

            //  //double finalSpadPredictor = 0;

            //  //if (validPixels.Count > 0)
            //  //{
            //  //    // 3. SORBARENDEZÉS (Ez a kulcs!)
            //  //    validPixels.Sort();

            //  //    //Trimmed mean
            //  //    // Eldobjuk az alsó és felső 25%ot
            //  //    int q1 = (int)(validPixels.Count * 0.25); 
            //  //    int q3 = (int)(validPixels.Count * 0.75); 

            //  //    // Biztonsági ellenőrzés, ha túl kicsi a minta
            //  //    if (q3 <= q1) { q1 = 0; q3 = validPixels.Count; }

            //  //    double sum = 0;
            //  //    int count = 0;

            //  //    // Csak a középső tartományt átlagoljuk
            //  //    for (int i = q1; i < q3; i++)
            //  //    {
            //  //        sum += validPixels[i];
            //  //        count++;
            //  //    }

            //  //    finalSpadPredictor = (count > 0) ? (sum / count) : 0;
            //  //}
            //  //else
            //  //{
            //  //    // Nem talált növényt a képen
            //  //    finalSpadPredictor = 0;
            //  //}
            //  #endregion
            //  // EZT AZ ÉRTÉKET használd a regresszióhoz és a méréshez is!
            //  // double estimatedSPAD = m * finalSpadPredictor + b;
            //  #region regi spad 


            //  //Cv2.MeanStdDev(NGRDI, out Scalar mgrviMeanRaw, out Scalar mgrviStdRaw, plantMask);
            //  //double? spadValue = null;



            //  //// SPAD = A * Index + B
            //  //spadValue = (PlantProfile.SpadSlope * mgrviMeanRaw.Val0) + PlantProfile.SpadIntercept;

            //  //if (spadValue < 0) spadValue = 0;
            //  #endregion
            //  var stats = new Snapshot
            //  {
            //      Timestamp = DateTime.Now,
            //      ImagePath = "n/a",
            //      VegetationIndexName = indexType.ToString(),
            //      ViMean = meanRaw.Val0,
            //      ViStdDev = stdRaw.Val0,
            //      PlantAreaPercentage = plantAreaPercentage,

            //  };

            //  return new ProcessingResult
            //  {
            //      ProcessedImageBytes = finalImage.ToBytes(".bmp"),
            //      Statistics = stats
            //  };
            #endregion
        }
        public byte[] GenerateColormapLegend()
        {
            // 256x1 szürke gradiens
            using Mat gradient = new Mat(1, 256, MatType.CV_8U);
            for (int i = 0; i < 256; i++)
            {
                gradient.Set<byte>(0, i, (byte)i);
            }

            //Felnagyitjuk
            using Mat resized = new Mat();
            Cv2.Resize(gradient, resized, new Size(512, 30), 0, 0, InterpolationFlags.Nearest);

            //Turbo szinezes
            using Mat colored = new Mat();
            Cv2.ApplyColorMap(resized, colored, ColormapTypes.Turbo);

            return colored.ToBytes(".bmp");
        }
        private Mat CalculateIndexImage(Mat original, VegetationIndex type)
        {
            using Mat b = new Mat();
            using Mat g = new Mat();
            using Mat r = new Mat();

            Cv2.Split(original, out Mat[] channels);
            channels[0].ConvertTo(b, MatType.CV_32F); // Blue
            channels[1].ConvertTo(g, MatType.CV_32F); // Green
            channels[2].ConvertTo(r, MatType.CV_32F); // Red

            using Mat sum = b + g + r;
            // Hozzáadunk egy picit, hogy ne osszunk nullával
            using Mat sumSafe = sum + new Scalar(0.001);

            // Kiszámoljuk a kisbetűs r, g, b értékeket (0..1 között lesznek)
            using Mat rNorm = r / sumSafe;
            using Mat gNorm = g / sumSafe;
            using Mat bNorm = b / sumSafe;

            foreach (var c in channels) c.Dispose(); //nincs szukseg a csatornakra mar

            // Az eredményt tároló mátrix
            Mat result = new Mat(original.Size(), MatType.CV_32F);
            switch (type)
            {
                case VegetationIndex.ExG:
                    // ExG = 2*G - R - B
                    using (Mat g2 = gNorm * 2)
                    using (Mat diff = g2 - rNorm)
                        Cv2.Subtract(diff, bNorm, result);
                    break;

                case VegetationIndex.ExGR:
                    // ExGR = ExG - ExR = (2G-R-B) - (1.4R-B)
                    // ExR = 1.4*R - G 
                    using (Mat exg = (gNorm * 2) - rNorm - bNorm)
                    using (Mat exr = (rNorm * 1.4) - gNorm)
                        Cv2.Subtract(exg, exr, result);
                    break;
                case VegetationIndex.VARI:
                    // VARI = (G - R) / (G + R - B)

                    using (Mat num = gNorm - rNorm)
                    using (Mat den1 = gNorm + rNorm)
                    using (Mat den2 = den1 - bNorm)
                    {
                        //nem szall el vegetelnbe
                        using (Mat denSafe = den2 + new Scalar(0.0000001))
                        {
                            Cv2.Divide(num, denSafe, result);
                        }
                    }
                    Cv2.PatchNaNs(result, 0);

                    // 2. Kiugró értékek levágása 
                    Cv2.Threshold(result, result, 1.0, 1.0, ThresholdTypes.Trunc);

                    // Minden, ami -1.0-nál kisebb, legyen -1.0
           
                    using (Mat lowerBound = new Mat(result.Size(), MatType.CV_32F, new Scalar(-1.0)))
                    {
                        Cv2.Max(result, lowerBound, result);
                    }
                    break;

                case VegetationIndex.NGRDI:
                    // NGRDI = (G - R) / (G + R)
                    using (Mat num = gNorm - rNorm)
                    using (Mat den = gNorm + rNorm)
                        Cv2.Divide(num, den, result);
                    break;

                case VegetationIndex.GLI:
                    // GLI = (2G - R - B) / (2G + R + B)
                    using (Mat g2 = gNorm * 2)
                    using (Mat num1 = g2 - rNorm)
                    using (Mat num = num1 - bNorm)
                    using (Mat den1 = g2 + rNorm)
                    using (Mat den = den1 + bNorm)
                        Cv2.Divide(num, den, result);
                    break;

                case VegetationIndex.TGI:
                    // TGI = G - 0.39*R - 0.61*B
                    using (Mat rW = rNorm * 0.39)
                    using (Mat bW = bNorm * 0.61)
                    using (Mat sub1 = gNorm - rW)
                        Cv2.Subtract(sub1, bW, result);
                    break;
                case VegetationIndex.MGRVI:
                    // MGRVI = (G^2 - R^2) / (G^2 + R^2)
                    using (Mat g2 = new Mat())
                    using (Mat r2 = new Mat())
                    {
                        Cv2.Pow(gNorm, 2, g2);
                        Cv2.Pow(rNorm, 2, r2);

                        using (Mat num = g2 - r2)
                        using (Mat den = g2 + r2)
                        {
                            using (Mat epsilon = new Mat(den.Size(), MatType.CV_32F, new Scalar(0.001)))
                            using (Mat safeDen = den + epsilon)
                            {
                                Cv2.Divide(num, safeDen, result);
                            }
                        }
                    }
                    break;

                default:
                    // Fallback: ExG
                    using (Mat g2 = gNorm * 2)
                    using (Mat diff = g2 - rNorm)
                        Cv2.Subtract(diff, bNorm, result);
                    break;
            }

            return result;

        }
        private Mat GetSegmentationMask(Mat original)
        {
            using Mat preMask = new Mat();
            using Mat blurred = new Mat();
            Cv2.GaussianBlur(original, blurred, new Size(5, 5), 0);
            // Előszegmentálás (ExG)


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
            int marginX = (int)(original.Cols * marginPercent );
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

            // 1/1 képméret, tiszta fekete
            Mat cropped = Mat.Zeros(original.Size(), original.Type());

            // Rámásoljuk a megvágott tartalmat 
            original.CopyTo(cropped, roiMask);

            return cropped;
        }
    }
}
