using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Core.Interfaces
{
    public interface ICameraService : IDisposable
    {
        bool IsRunning { get; }

        // Indítás kamerával (index)
        void Start(int cameraIndex = 0);

        // Indítás videófájllal
        void Start(string videoPath);

        // Leállítás
        void Stop();

        // Események, amikre a ViewModel feliratkozhat
        event EventHandler<Mat> FrameCaptured; // Jön egy új képkocka
        event EventHandler<string> ErrorOccurred; // Baj van
    }
}
