﻿#region Mr. Advice
// Mr. Advice
// A simple post build weaving package
// http://mradvice.arxone.com/
// Released under MIT license http://opensource.org/licenses/mit-license.php
#endregion

namespace ArxOne.MrAdvice
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using dnlib.DotNet;
    using IO;
    using Reflection;
    using StitcherBoy;
    using StitcherBoy.Logging;
    using StitcherBoy.Weaving.Build;
    using Weaver;

    public class MrAdviceStitcher : AssemblyStitcher
    {
        private ILogging _logging;

        protected override bool Process(AssemblyStitcherContext context)
        {
            if (AlreadyProcessed(context))
                return false;

#if DEBUG
            _logging = new MultiLogging(new DefaultLogging(Logging), new FileLogging("MrAdvice.log"));
            _logging.WriteDebug("Start");
#else
            _logging = Logging;
#endif
            try
            {
                // instances are created here
                // please also note poor man's dependency injection (which is enough for us here)
                var assemblyResolver = context.AssemblyResolver;
                var typeResolver = new TypeResolver { Logging = _logging, AssemblyResolver = assemblyResolver };
                var typeLoader = new TypeLoader(() => LoadWeavedAssembly(context, assemblyResolver));
                var aspectWeaver = new AspectWeaver { Logging = _logging, TypeResolver = typeResolver, TypeLoader = typeLoader };
                // TODO: use blobber's resolution (WTF?)
                AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                BlobberHelper.Setup();

                return aspectWeaver.Weave(context.Module);
            }
            catch (Exception e)
            {
                _logging.WriteError("Internal error: {0}", e.ToString());
                for (var ie = e.InnerException; ie != null; ie = ie.InnerException)
                    _logging.WriteError("Inner exception: {0}", e.ToString());
            }
            return false;
        }

        private bool AlreadyProcessed(AssemblyStitcherContext context)
        {
            var processFilePath = GetProcessFilePath(context);
            var processed = GetLastWriteDate(processFilePath) >= GetLastWriteDate(context.AssemblyPath);
            if (!processed)
            {
                ModuleWritten += delegate
                {
                    File.WriteAllText(processFilePath,
                        "This file is a marker for Mr.Advice to ensure the assembly wan't processed twice (in which case it would be as bad as crossing the streams).");
                };
            }
            return processed;
        }

        private static string GetProcessFilePath(AssemblyStitcherContext context)
        {
            return context.AssemblyPath + ".\u2665MrAdvice";
        }

        private static DateTime GetLastWriteDate(string path)
        {
            if (!File.Exists(path))
                return DateTime.MinValue;
            return new FileInfo(path).LastWriteTimeUtc;
        }

        private Assembly OnAssemblyResolve(object sender, ResolveEventArgs args) => LoadAssembly(new AssemblyName(args.Name).Name);

        private Assembly LoadAssembly(string assemblyName)
        {
            // because versions may differ, we'll pretend they're all the same
            if (assemblyName == "MrAdvice")
                return GetType().Assembly;


            // otherwise fallback to embedded resources,
            // which for some fucking reason are not resolved by Blobber!
            var assemblyData = ResolveAssembly(assemblyName);
            if (assemblyData == null)
                return null;

            return Assembly.Load(assemblyData);
        }

        /// <summary>
        /// Resolves the assembly.
        /// </summary>
        /// <param name="assemblyName">Name of the assembly.</param>
        /// <returns></returns>
        private static byte[] ResolveAssembly(string assemblyName)
        {
            var resourceName = $"blobber:embedded.gz:{assemblyName}";

            using (var resourceStream = typeof(MrAdviceStitcher).Assembly.GetManifestResourceStream(resourceName))
            {
                if (resourceStream == null)
                    return null;
                using (var gzipStream = new GZipStream(resourceStream, CompressionMode.Decompress))
                using (var memoryStream = new MemoryStream())
                {
                    gzipStream.CopyTo(memoryStream);
                    return memoryStream.ToArray();
                }
            }
        }

        /// <summary>
        /// Loads the weaved assembly.
        /// </summary>
        /// <returns></returns>
        private Assembly LoadWeavedAssembly(AssemblyStitcherContext context, IAssemblyResolver assemblyResolver)
        {
            foreach (var assemblyRef in context.Module.GetAssemblyRefs())
            {
                try
                {
                    var assemblyRefName = new AssemblyName(assemblyRef.FullName);
                    if (assemblyRefName.Name == "MrAdvice")
                        continue;
                    var referencePath = assemblyResolver.Resolve(assemblyRef, context.Module).ManifestModule.Location;
                    var fileName = Path.GetFileName(referencePath);
                    // right, this is dirty!
                    if (fileName == "MrAdvice.dll" && AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "MrAdvice"))
                        continue;

                    //if (string.IsNullOrEmpty(referencePath))
                    //{
                    //    Logger.WriteDebug("Loading assembly from {0}", referencePath);
                    //}

                    var referenceBytes = File.ReadAllBytes(referencePath);
                    Assembly.Load(referenceBytes);
                }
                catch (Exception e)
                {
                    _logging.WriteWarning("Can't load {0}: {1}", assemblyRef.FullName, e.ToString());
                }
            }
            var bytes = File.ReadAllBytes(context.Module.Assembly.ManifestModule.Location);
            return Assembly.Load(bytes);
        }
    }
}
