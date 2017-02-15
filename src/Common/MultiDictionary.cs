using System;
using System.Collections.Generic;

namespace Microsoft.SourceBrowser.Common
{
    public class MultiDictionary<K, V> : Dictionary<K, HashSet<V>>
    {
        private IEqualityComparer<V> valueComparer;

        public MultiDictionary()
        {
        }

        public MultiDictionary(IEqualityComparer<K> keyComparer, IEqualityComparer<V> valueComparer)
            : base(keyComparer)
        {
            this.valueComparer = valueComparer;
        }

        public void Add(K key, V value)
        {
            if (EqualityComparer<K>.Default.Equals(default(K), key))
            {
                throw new ArgumentNullException("key");
            }

            HashSet<V> bucket = null;
            if (!TryGetValue(key, out bucket))
            {
                bucket = new HashSet<V>(valueComparer);
                this.Add(key, bucket);
            }

            bucket.Add(value);
        }
    }
}
