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

      var stateIndependentProjection = new StateIndependentProjection(
        input: new ProjectionInput(),//TODO MAKE THE CASH FLOWS GREAT AGAIN!
        ecoScenarioGenerator: new EconomicScenarioGenerator(),
        numberOfEconomicScenarios: 1);

      var balanceAndResults = stateIndependentProjection.Project();

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
