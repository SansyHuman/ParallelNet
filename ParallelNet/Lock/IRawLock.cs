using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ParallelNet.Common;

namespace ParallelNet.Lock
{
    /// <summary>
    /// Raw lock interface.
    /// </summary>
    /// <typeparam name="Token">Type of token</typeparam>
    public interface IRawLock<Token>
    {
        /// <summary>
        /// Acquires the raw lock.
        /// </summary>
        /// <returns>Token after acquiring lock</returns>
        Token Lock();

        /// <summary>
        /// Releases the raw lock.
        /// </summary>
        /// <param name="token">Token acquired when acquiring raw lock</param>
        void Unlock(Token token);
    }

    /// <summary>
    /// Raw lock interface with TryLock method.
    /// </summary>
    /// <typeparam name="Token"></typeparam>
    public interface IRawTryLock<Token> : IRawLock<Token>
    {
        /// <summary>
        /// Tries to acquire the raw lock.
        /// </summary>
        /// <returns>If succeeded, the token value. Else, none.</returns>
        Result<Token, None> TryLock();
    }
}
