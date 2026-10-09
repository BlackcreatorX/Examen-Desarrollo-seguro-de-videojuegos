using System;
using System.IO;
using UnityEngine;

namespace Juego.Guardado
{
    /// Dato de ejemplo; amplíalo con los campos del juego.
    [Serializable]
    public sealed class SaveData
    {
        public ulong coins;
        public int level;
    }

    /// Estado compartido por los eslabones de la cadena.
    public sealed class SaveContext
    {
        public SaveData Raw { get; set; }
        public ulong Transformed { get; set; }
        public bool IsValid { get; set; } = true;
        public string Error { get; set; }
        public string FilePath { get; set; }
        public uint IntegrityTag { get; set; }

        public SaveContext(SaveData raw, string filePath)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        }
    }

    /// Base CoR; SetNext permite construir la cadena fluidamente.
    public abstract class SaveHandler
    {
        private SaveHandler next;

        public SaveHandler SetNext(SaveHandler handler)
        {
            next = handler ?? throw new ArgumentNullException(nameof(handler));
            return handler;
        }

        public void Handle(SaveContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!context.IsValid) return;
            Process(context);
            if (context.IsValid) next?.Handle(context);
        }

        protected abstract void Process(SaveContext context);
    }

    /// Valida campos de dominio y etiqueta de integridad antes de guardar.
    public sealed class Validator : SaveHandler
    {
        protected override void Process(SaveContext c)
        {
            if (c.Raw == null || string.IsNullOrWhiteSpace(c.FilePath) || c.Raw.level < 0)
            {
                c.IsValid = false;
                c.Error = "Datos incompletos o nivel inválido.";
                return;
            }
            c.IntegrityTag = Integrity(c.Raw);
        }

        internal static uint Integrity(SaveData d)
        {
            unchecked
            {
                uint h = 2166136261;
                ulong n = d.coins;
                for (int i = 0; i < 8; i++) { h = (h ^ (byte)n) * 16777619; n >>= 8; }
                uint level = (uint)d.level;
                for (int i = 0; i < 4; i++) { h = (h ^ (byte)level) * 16777619; level >>= 8; }
                return h;
            }
        }
    }

    /// 
    /// Permutación Feistel de 64 bits: no lineal y exactamente reversible.
    /// Es ofuscación, no cifrado criptográfico. Las claves son solo RAM.
    /// 
    public sealed class Obfuscator : SaveHandler
    {
        private const int Rounds = 8;
        private readonly uint[] roundKeys = new uint[Rounds];
        private readonly object gate = new object();

        public Obfuscator() { RotateKeys(); }

        // Sustituye la clave completa; no se escribe en disco.
        public void RotateKeys()
        {
            lock (gate)
                for (int i = 0; i < Rounds; i++)
                {
                    byte[] b = new byte[4];
                    using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create()) rng.GetBytes(b);
                    roundKeys[i] = BitConverter.ToUInt32(b, 0);
                }
        }

        // Captura temporal para que la prueba pueda validar datos creados con la clave anterior.
        // Mantener esta copia permite descifrar tras la rotación, pero también prolonga su exposición.
        public uint[] CaptureKeySnapshot() { lock (gate) return (uint[])roundKeys.Clone(); }

        protected override void Process(SaveContext c)
        {
            uint[] keys = CaptureKeySnapshot();
            c.Transformed = Transform(c.Raw.coins, keys);
        }

        public ulong Restore(ulong value, uint[] keySnapshot) => Inverse(value, keySnapshot);

        public static ulong Transform(ulong value, uint[] keys)
        {
            CheckKeys(keys);
            uint left = (uint)(value >> 32), right = (uint)value;
            unchecked
            {
                for (int i = 0; i < Rounds; i++) { uint oldRight = right; right = left ^ Round(right, keys[i]); left = oldRight; }
            }
            return ((ulong)left << 32) | right;
        }

        public static ulong Inverse(ulong value, uint[] keys)
        {
            CheckKeys(keys);
            uint left = (uint)(value >> 32), right = (uint)value;
            unchecked
            {
                for (int i = Rounds - 1; i >= 0; i--) { uint oldLeft = right; right = left ^ Round(right, keys[i]); left = oldLeft; }
            }
            return ((ulong)left << 32) | right;
        }

        private static uint Round(uint x, uint k)
        {
            unchecked
            {
                x ^= k;
                x *= 0x9E3779B1u; // impar: permutación módulo 2^32
                x ^= x >> 16;
                x *= 0x85EBCA6Bu;
                x ^= x >> 13;
                return (x << 7) | (x >> 25);
            }
        }

        private static void CheckKeys(uint[] keys)
        {
            if (keys == null || keys.Length != Rounds) throw new ArgumentException("Se requieren ocho subclaves.", nameof(keys));
        }
    }

    [Serializable]
    internal sealed class SaveEnvelope
    {
        public ulong coins;
        public int level;
        public uint integrityTag;
    }

    /// Persiste JSON en Application.persistentDataPath.
    public sealed class Persister : SaveHandler
    {
        protected override void Process(SaveContext c)
        {
            try
            {
                string dir = Path.GetDirectoryName(c.FilePath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                var envelope = new SaveEnvelope { coins = c.Transformed, level = c.Raw.level, integrityTag = c.IntegrityTag };
                File.WriteAllText(c.FilePath, JsonUtility.ToJson(envelope, true));
            }
            catch (Exception e) { c.IsValid = false; c.Error = "No se pudo guardar: " + e.Message; }
        }
    }

    /// Composición que puede poseer GameManager y ejecutar al guardar.
    public sealed class SecureSavePipeline
    {
        private readonly SaveHandler chain;
        public Obfuscator Obfuscator { get; }

        public SecureSavePipeline(string path)
        {
            Obfuscator = new Obfuscator();
            var validator = new Validator();
            validator.SetNext(Obfuscator).SetNext(new Persister());
            chain = validator;
            Path = path;
        }

        public string Path { get; }
        public SaveContext Save(SaveData data)
        {
            var context = new SaveContext(data, Path);
            chain.Handle(context);
            return context;
        }
    }

    /// Ejemplo invocable desde GameManager.Awake/una escena de prueba.
    public sealed class SavePipelineDemo : MonoBehaviour
    {
        private void Start()
        {
            string path = System.IO.Path.Combine(Application.persistentDataPath, "save.json");
            var pipeline = new SecureSavePipeline(path);
            const ulong original = 12345678901234567890UL;
            uint[] oldKeys = pipeline.Obfuscator.CaptureKeySnapshot();
            SaveContext result = pipeline.Save(new SaveData { coins = original, level = 3 });
            if (!result.IsValid) { Debug.LogError(result.Error); return; }

            pipeline.Obfuscator.RotateKeys();
            ulong recovered = pipeline.Obfuscator.Restore(result.Transformed, oldKeys);
            Debug.Log($"Original={original}; transformado={result.Transformed}; recuperado={recovered}; iguales={original == recovered}");
            Debug.Log("JSON guardado en: " + path);
        }
    }
}
