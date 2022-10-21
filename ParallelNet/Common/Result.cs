using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Common
{
    /// <summary>
    /// Class that contains success and failure results.
    /// </summary>
    /// <typeparam name="Success">Type of success result</typeparam>
    /// <typeparam name="Failure">Type of error result</typeparam>
    public struct Result<Success, Failure>
    {
        /// <summary>
        /// Type of the result.
        /// </summary>
        public enum Type
        {
            /// <summary>
            /// The result is success type.
            /// </summary>
            Success,

            /// <summary>
            /// The result is error type.
            /// </summary>
            Failure
        }

        private Success? success;
        private Failure? failure;
        private Type type;

        private Result(Success? success, Failure? failure, Type type)
        {
            this.success = success;
            this.failure = failure;
            this.type = type;
        }

        /// <summary>
        /// Builds succeeded result.
        /// </summary>
        /// <param name="result">Succeeded result value</param>
        /// <returns>Result</returns>
        public static Result<Success, Failure> Suceeded(in Success result)
        {
            return new Result<Success, Failure>(result, default, Type.Success);
        }

        /// <summary>
        /// Builds failed result.
        /// </summary>
        /// <param name="result">Error value</param>
        /// <returns>Result</returns>
        public static Result<Success, Failure> Failed(in Failure error)
        {
            return new Result<Success, Failure>(default, error, Type.Failure);
        }

        /// <summary>
        /// Gets type of the result.
        /// </summary>
        public Type ResultType => type;

        /// <summary>
        /// Gets succeeded value of the result if succeeded, or default value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the result is <typeparamref name="Failure"/>.</exception>
        public Success? ResultValue
        {
            get
            {
                if (type == Type.Success)
                    return success;
                else
                    throw new InvalidOperationException("The result is not succeess");
            }
        }

        /// <summary>
        /// Gets error value of the result if failed, or default value.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the result is <typeparamref name="Success"/>.</exception>
        public Failure? ErrorValue
        {
            get
            {
                if (type == Type.Failure)
                    return failure;
                else
                    throw new InvalidOperationException("The result is not failure");
            }
        }

        public static implicit operator Result<Success, Failure>(in Success result) => Result<Success, Failure>.Suceeded(result);
        public static explicit operator Result<Success, Failure>(in Failure error) => Result<Success, Failure>.Failed(error);
    }
}
