using Microsoft.ML.OnnxRuntime;
using System;
using System.Reflection;

namespace SortMyMedia.FaceRecognition
{
    public static class OnnxRuntimeSessionFactory
    {
        public static InferenceSession Create(string modelPath, string modelTag, out bool usingCuda)
        {
            try
            {
                var cudaOptions = CreateCudaSessionOptions();
                ConfigureSessionOptions(cudaOptions);
                var cudaSession = new InferenceSession(modelPath, cudaOptions);
                usingCuda = true;
                Console.WriteLine($"{modelTag}: ONNX Runtime using CUDAExecutionProvider.");
                return cudaSession;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{modelTag}: CUDA unavailable, falling back to CPUExecutionProvider. {ex.Message}");
                using var cpuOptions = new SessionOptions();
                ConfigureSessionOptions(cpuOptions);
                usingCuda = false;
                Console.WriteLine($"{modelTag}: ONNX Runtime using CPUExecutionProvider.");
                return new InferenceSession(modelPath, cpuOptions);
            }
        }

        private static void ConfigureSessionOptions(SessionOptions options)
        {
            options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
            options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;
            options.EnableCpuMemArena = true;
            options.EnableMemoryPattern = true;
        }

        private static SessionOptions CreateCudaSessionOptions()
        {
            var options = new SessionOptions();

            MethodInfo? appendCudaWithDevice = typeof(SessionOptions).GetMethod("AppendExecutionProvider_CUDA", new[] { typeof(int) });
            if (appendCudaWithDevice != null)
            {
                appendCudaWithDevice.Invoke(options, new object[] { 0 });
                return options;
            }

            MethodInfo? appendCudaNoArgs = typeof(SessionOptions).GetMethod("AppendExecutionProvider_CUDA", Type.EmptyTypes);
            if (appendCudaNoArgs != null)
            {
                appendCudaNoArgs.Invoke(options, null);
                return options;
            }

            MethodInfo? staticCudaFactory = typeof(SessionOptions).GetMethod("MakeSessionOptionWithCudaProvider", BindingFlags.Public | BindingFlags.Static);
            if (staticCudaFactory != null)
            {
                object? result;
                var parameters = staticCudaFactory.GetParameters();
                if (parameters.Length == 0)
                    result = staticCudaFactory.Invoke(null, null);
                else
                    result = staticCudaFactory.Invoke(null, new object[] { 0 });

                if (result is SessionOptions factoryOptions)
                {
                    options.Dispose();
                    return factoryOptions;
                }
            }

            options.Dispose();
            throw new NotSupportedException("CUDA provider methods are not available in this ONNX Runtime build.");
        }
    }
}
