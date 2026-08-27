using System;
using ICities;
using UnityEngine;

namespace StopStacker
{
    public sealed class StopStackerSerializableExtension : SerializableDataExtensionBase
    {
        private const string DataId = "StopStacker.DisabledStops.v1";

        public override void OnLoadData()
        {
            base.OnLoadData();

            try
            {
                byte[] data = serializableDataManager == null ? null : serializableDataManager.LoadData(DataId);
                StopStackerDisabledStops.Restore(data);
                StopStackerDisabledStops.LogState("DISABLED_STOPS_RESTORED");
            }
            catch (Exception e)
            {
                StopStackerDisabledStops.Restore(null);
                Debug.LogError("[StopStacker] DISABLED_STOPS_RESTORE_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
        }

        public override void OnSaveData()
        {
            base.OnSaveData();

            try
            {
                if (serializableDataManager == null)
                    return;

                serializableDataManager.SaveData(DataId, StopStackerDisabledStops.Serialize());
                StopStackerDisabledStops.LogState("DISABLED_STOPS_SAVED");
            }
            catch (Exception e)
            {
                Debug.LogError("[StopStacker] DISABLED_STOPS_SAVE_FAILED: " + e.GetType().Name + ": " + e.Message);
            }
        }
    }
}
