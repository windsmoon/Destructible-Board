using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windsmoon.DesctructibleBoard
{
    [Serializable]
    internal sealed class ColliderCellIndexDictionary : Dictionary<Collider, int>, ISerializationCallbackReceiver
    {
        #region fields
        [SerializeField]
        private List<Collider> _keyList = new List<Collider>();
        [SerializeField]
        private List<int> _valueList = new List<int>();
        #endregion

        #region methods
        /// <summary>
        /// Saves the current mapping using lists supported by Unity serialization.
        /// </summary>
        public void OnBeforeSerialize()
        {
            _keyList.Clear();
            _valueList.Clear();
            foreach (KeyValuePair<Collider, int> pair in this)
            {
                _keyList.Add(pair.Key);
                _valueList.Add(pair.Value);
            }
        }

        /// <summary>
        /// Restores the saved mapping without searching fragment objects.
        /// </summary>
        public void OnAfterDeserialize()
        {
            Clear();
            int count = Math.Min(_keyList.Count, _valueList.Count);
            for (int index = 0; index < count; index++)
            {
                // Serialization callbacks can run off the main thread; avoid Unity's null operator.
                if (ReferenceEquals(_keyList[index], null) == false)
                {
                    this[_keyList[index]] = _valueList[index];
                }
            }
        }
        #endregion
    }
}
