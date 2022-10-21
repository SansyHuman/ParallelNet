using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Common
{
    /// <summary>
    /// Class that contains optional value.
    /// </summary>
    /// <typeparam name="T">Type of optional value</typeparam>
    public struct Option<T>
    {
        /// <summary>
        /// Status of Option object
        /// </summary>
        public enum Status
        {
            /// <summary>
            /// There is a value in the object.
            /// </summary>
            Some,

            /// <summary>
            /// There is no value in the object.
            /// </summary>
            None
        }

        private T? value;
        private Status status;

        private Option(in T? value, Status status)
        {
            this.value = value;
            this.status = status;
        }

        /// <summary>
        /// Creates Some value.
        /// </summary>
        /// <param name="value">Value in the option</param>
        /// <returns>Option</returns>
        public static Option<T> Some(in T value)
        {
            return new Option<T>(value, Status.Some);
        }

        /// <summary>
        /// Creates None value.
        /// </summary>
        /// <returns>Option</returns>
        public static Option<T> None()
        {
            return new Option<T>(default, Status.None);
        }

        /// <summary>
        /// Gets the status of the option.
        /// </summary>
        public Status OptionStatus => status;

        /// <summary>
        /// Gets the value if the option contains the value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the option is None.</exception>
        public T? Value
        {
            get
            {
                if (status == Status.Some)
                    return value;
                else
                    throw new InvalidOperationException("There is no value in the option");
            }
        }

        public static implicit operator Option<T>(in T value) => Option<T>.Some(value);
    }
}
