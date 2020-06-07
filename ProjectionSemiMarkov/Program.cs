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

      //TODO implement a economic scenario generator or read from Hemo R-file
      var ecoSencarios = new List<string> { "scen1", "scen2", "scen2" };

      //TODO MAKE THE CASH FLOWS GREAT AGAIN!
      var projectionInput = new ProjectionInput();

      foreach (var ecoScenario in ecoSencarios)
      {
        var stateIndependentProjection = new StateIndependentProjection(projectionInput, ecoScenario);

      }

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
