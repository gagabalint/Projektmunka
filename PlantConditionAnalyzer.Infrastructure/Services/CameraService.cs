using OpenCvSharp;
using PlantConditionAnalyzer.Core.Interfaces;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PlantConditionAnalyzer.Infrastructure.Services
{
    public class CameraService : ICameraService
    {
        private VideoCapture? capture;
        private CancellationTokenSource? cts;
        private Task? workerTask;
        private readonly object cameraLock = new object();

        public bool IsRunning { get; private set; }

        // Események
        public event EventHandler<Mat>? FrameCaptured;
        public event EventHandler<string>? ErrorOccurred;

        public void Start(int cameraIndex = 0)
        {
            Stop(); // Biztonsági leállítás
            try
            {
                capture = new VideoCapture(cameraIndex);
                StartLoop();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Camera Init Error: {ex.Message}");
            }
        }

        public void Start(string videoPath)
        {
            Stop();
            try
            {
                capture = new VideoCapture(videoPath);
                StartLoop();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, $"Video Init Error: {ex.Message}");
            }
        }

        private void StartLoop()
        {
            if (capture == null || !capture.IsOpened())
            {
                ErrorOccurred?.Invoke(this, "Could not open camera/video.");
                return;
            }

            IsRunning = true;
            cts = new CancellationTokenSource();

            // Elindítjuk a végtelen ciklust egy külön szálon
            workerTask = Task.Run(() => CaptureLoop(cts.Token));
        }

        private void CaptureLoop(CancellationToken token)
        {
            using Mat frame = new Mat();

            while (!token.IsCancellationRequested && capture != null && capture.IsOpened())
            {
                bool readSuccess = false;

                // Kritikus szakasz: Olvasás
                lock (cameraLock)
                {
                    // Ha időközben leállították
                    if (capture == null || capture.IsDisposed) break;
                    readSuccess = capture.Read(frame);
                }

                if (readSuccess && !frame.Empty())
                {
                    // Jelezzük a külvilágnak (ViewModel), hogy van kép.
                    // Fontos: Klónozzuk, hogy a ViewModel szabadon használhassa,
                    // miközben mi már a következőt olvassuk.
                    FrameCaptured?.Invoke(this, frame.Clone());
                }
                else
                {
                    // Ha videófájl volt és vége, vagy hiba történt
                    // (Élő kameránál ritka a false, de előfordulhat)
                    if (!token.IsCancellationRequested)
                    {
                        // Opcionális: ErrorOccurred?.Invoke(this, "End of stream");
                        Stop(); // Leállítjuk magunkat
                    }
                    break;
                }

                // Kb. 30 FPS (33ms várakozás)
                Thread.Sleep(33);
            }
        }

        public void Stop()
        {
            IsRunning = false;
            cts?.Cancel(); // Jelezzük a szálnak, hogy álljon le

            // Várunk kicsit, hogy a szál befejezze
            // (A .Wait() itt veszélyes lehet UI szálon, de mivel rövid a ciklus, elmegy,
            // vagy hagyjuk futni, a lock úgyis véd)

            lock (cameraLock)
            {
                if (capture != null && !capture.IsDisposed)
                {
                    capture.Release();
                    capture.Dispose();
                }
                capture = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}