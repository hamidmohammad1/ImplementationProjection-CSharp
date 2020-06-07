using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ProjectionSemiMarkov
{
  class Program
  {
    static void Main()
    {
      var stopWatch = new Stopwatch();
      stopWatch.Start();

      var ecoScenarioGenerator = new EconomicScenarioGenerator();
      var projectionInput = new ProjectionInput(); //TODO MAKE THE CASH FLOWS GREAT AGAIN!

      for (var i = 0; i < 1; i++)
      {
        var stateIndependentProjection = new StateIndependentProjection(
          projectionInput,
          ecoScenarioGenerator.SimulateMarket(),
          (r, t, T) => ecoScenarioGenerator.ZeroCouponBondPrices(r, t, T));

        stateIndependentProjection.Project();
      }

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
