using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Wivuu.GlobalCache
{
    public class RetryHelper : IDisposable
    {
        public RetryHelper(int initialDelay = 500, int maxDelay = 5_000, int? maxTries = null, TimeSpan? totalMaxDelay = null)
        {
            this.DelayEnumerator = Delays().GetEnumerator();

            IEnumerable<int> Delays()
            {
                var random = new Random();
                var tries = 0;
                
                while (true)
                {
                    var proposed = Math.Min((int)Math.Pow(2, tries) + initialDelay, maxDelay);
                    var wiggle   = random.Next(0, 5);

                    yield return proposed + wiggle;

                    if (tries++ > maxTries == true)
                        yield break;
                }
            }

            this.TotalMaxDelay = totalMaxDelay;
        }

        private TimeSpan TotalDelay { get; set; }
        private TimeSpan? TotalMaxDelay { get; }
        private IEnumerator<int> DelayEnumerator { get; }

        public async Task<bool> DelayAsync()
        {
            if (this.DelayEnumerator.MoveNext() == false)
                return false;

            var delay = this.DelayEnumerator.Current;

            if (delay > 0)
                await Task.Delay(delay);

            TotalDelay += TimeSpan.FromMilliseconds(delay);
            if (TotalDelay > TotalMaxDelay)
                return false;

            return true;
        }

        public void Dispose() => DelayEnumerator.Dispose();
    }
}