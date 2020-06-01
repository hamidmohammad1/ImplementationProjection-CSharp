using System;
using System.Diagnostics;

namespace ProjectionSemiMarkov
{
  class Program
  {
    static void Main()
    {
      var stopWatch = new Stopwatch();
      stopWatch.Start();

      var projectionInput = new Projection();

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
