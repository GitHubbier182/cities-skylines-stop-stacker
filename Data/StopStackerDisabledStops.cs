using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace StopStacker
{
    internal static class StopStackerDisabledStops
    {
        private const int SerializationVersion = 1;

        private static readonly HashSet<ushort> DisabledStops = new HashSet<ushort>();
        private static readonly object DisabledStopsLock = new object();

        public static int Count
        {
            get
            {
                lock (DisabledStopsLock)
                    return DisabledStops.Count;
            }
        }

        public static bool IsDisabled(ushort stopNode)
        {
            lock (DisabledStopsLock)
                return stopNode != 0 && DisabledStops.Contains(stopNode);
        }

        public static bool SetDisabled(ushort stopNode, bool disabled)
        {
            if (stopNode == 0)
                return false;

            lock (DisabledStopsLock)
                return disabled ? DisabledStops.Add(stopNode) : DisabledStops.Remove(stopNode);
        }

        public static bool ResetAll()
        {
            lock (DisabledStopsLock)
            {
                if (DisabledStops.Count == 0)
                    return false;

                DisabledStops.Clear();
                return true;
            }
        }

        public static int PruneToKnownStops(HashSet<ushort> knownStops)
        {
            lock (DisabledStopsLock)
            {
                if (DisabledStops.Count == 0 || knownStops == null)
                    return 0;

                List<ushort> staleStops = null;
                foreach (ushort stopNode in DisabledStops)
                {
                    if (knownStops.Contains(stopNode))
                        continue;

                    if (staleStops == null)
                        staleStops = new List<ushort>();

                    staleStops.Add(stopNode);
                }

                if (staleStops == null)
                    return 0;

                for (int i = 0; i < staleStops.Count; i++)
                    DisabledStops.Remove(staleStops[i]);

                return staleStops.Count;
            }
        }

        public static byte[] Serialize()
        {
            lock (DisabledStopsLock)
            {
                using (MemoryStream stream = new MemoryStream())
                {
                    using (BinaryWriter writer = new BinaryWriter(stream))
                    {
                        writer.Write(SerializationVersion);
                        writer.Write(DisabledStops.Count);
                        foreach (ushort stopNode in DisabledStops)
                            writer.Write(stopNode);
                    }

                    return stream.ToArray();
                }
            }
        }

        public static void Restore(byte[] data)
        {
            lock (DisabledStopsLock)
            {
                DisabledStops.Clear();
                if (data == null || data.Length == 0)
                    return;

                using (MemoryStream stream = new MemoryStream(data))
                {
                    using (BinaryReader reader = new BinaryReader(stream))
                    {
                        int version = reader.ReadInt32();
                        if (version != SerializationVersion)
                            return;

                        int count = reader.ReadInt32();
                        if (count < 0 || count > ushort.MaxValue)
                            return;

                        long requiredBytes = count * sizeof(ushort);
                        if (stream.Length - stream.Position < requiredBytes)
                            return;

                        for (int i = 0; i < count; i++)
                        {
                            ushort stopNode = reader.ReadUInt16();
                            if (stopNode != 0)
                                DisabledStops.Add(stopNode);
                        }
                    }
                }
            }
        }

        public static void LogState(string eventName)
        {
            StopStackerDiagnostics.Advanced("[StopStacker] " + eventName + ": disabledStops=" + DisabledStops.Count);
        }
    }
}
