using System;
using System.Diagnostics;


namespace ProjectionSemiMarkov
{
  class Program
  {
    static void Main(string[] args)
    {
      var stopWatch = new Stopwatch();
      stopWatch.Start();

      var marketBasis = Setup.CreateMarketBasisIntensities();
      var technicalBasis = Setup.CreateTechnicalBasisIntensities();
      var policies = Setup.CreatePolicies();

      var marketProbabilityCalculator = new ProbabilityCalculator(marketBasis, policies);
      var marketProb = marketProbabilityCalculator.Calculate();

      var technicalReserveCalculator = new TechnicalReserveCalculator(technicalBasis.Item1, technicalBasis.Item2, policies);
      var techReserves = technicalReserveCalculator.Calculate();

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds / 1000;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
