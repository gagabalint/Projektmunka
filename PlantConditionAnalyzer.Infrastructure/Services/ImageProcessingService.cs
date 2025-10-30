using OpenCvSharp;
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
        public ProcessingResult ProcessImage(string imagePath)
        {
            using (Mat original = Cv2.ImRead(imagePath, ImreadModes.Color))
            {
                // Tegyünk rá egy szöveget, hogy lássuk, feldolgozta
                Cv2.PutText(original, "FELDOLGOZVA!",
                    new Point(10, 50), HersheyFonts.HersheySimplex, 2, Scalar.White, 3);

                // Alakítsuk át bájtokká (.bmp-be a legegyszerűbb)
                byte[] imageBytes = original.ToBytes(".bmp");

                var stats = new Snapshot
                {
                    Timestamp = DateTime.Now,
                    ImagePath = "teszt.png",
                    ViMean = 0.5,
                    ViStdDev = 0.1,
                    PlantAreaPercentage = 50
                };
                return new ProcessingResult
                {
                    ProcessedImageBytes = imageBytes,
                    Statistics = stats
                };
            }
        }
    }
}
