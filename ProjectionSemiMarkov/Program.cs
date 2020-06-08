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

      stateIndependentProjection.Project();

      var result = stateIndependentProjection.ProjectionResult;
      //TODO NOW CALCULATE BALANCE QUANTITIES. Should be easy.

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
