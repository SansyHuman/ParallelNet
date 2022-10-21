using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Common
{
    /// <summary>
    /// Dummy class that represents no data.
    /// </summary>
    public class None
    {
        private static Lazy<None> none = new Lazy<None>(() => new None());

        /// <summary>
        /// Gets dummy instance of None value. This is singleton and thread-safe.
        /// </summary>
        public static None Value => None.none.Value;
    }
}
