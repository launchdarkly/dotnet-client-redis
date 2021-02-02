using System;
using System.Collections.Generic;

namespace LaunchDarkly.Client.SharedTests.FeatureStore
{
    /// <summary>
    /// Simplifies building the input parameter for a feature store's Init method.
    /// </summary>
    public class DataBuilder
    {
        private readonly IDictionary<IVersionedDataKind, IDictionary<String, IVersionedData>> _data =
            new Dictionary<IVersionedDataKind, IDictionary<String, IVersionedData>>();

        public DataBuilder Add(IVersionedDataKind kind, params IVersionedData[] items)
        {
            IDictionary<String, IVersionedData> itemsDict;
            if (!_data.TryGetValue(kind, out itemsDict))
            {
                itemsDict = new Dictionary<String, IVersionedData>();
                _data[kind] = itemsDict;
            }
            foreach (var item in items)
            {
                itemsDict[item.Key] = item;
            }
            return this;
        }

        public IDictionary<IVersionedDataKind, IDictionary<String, IVersionedData>> Build()
        {
            return _data;
        }
    }
}
