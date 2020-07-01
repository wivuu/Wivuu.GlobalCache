using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    internal class RetryHelper
    {
        public RetryHelper(int initialDelay = 500,
                           int maxDelay = 5_000,
                           int? maxTries = null,
                           TimeSpan? totalMaxDelay = null)
        {
            this.deadline      = DateTimeOffset.UtcNow + totalMaxDelay;
            this.initialDelay  = initialDelay;
            this.maxDelay      = maxDelay;
            this.maxTries      = maxTries;
            this.totalMaxDelay = totalMaxDelay;
        }

        private int tries = 0;
        private readonly DateTimeOffset? deadline;
        private readonly int initialDelay;
        private readonly int maxDelay;
        private readonly int? maxTries;
        private readonly TimeSpan? totalMaxDelay;
        private readonly Random random = new Random();

        public async Task<bool> DelayAsync(CancellationToken cancellationToken = default)
        {
            if (tries >= maxTries == true || DateTimeOffset.UtcNow > deadline)
                return false;

            var proposed = Math.Min((2 << tries++) + initialDelay, maxDelay);
            var wiggle   = random.Next(0, 3);
            var wait     = proposed + wiggle;

            if (wait < 2)
                await Task.Yield();
            else
                await Task.Delay(wait, cancellationToken);

            return true;
        }
    }
}