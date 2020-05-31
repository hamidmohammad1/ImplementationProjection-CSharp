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
      marketProbabilityCalculator.Calculate();

      var technicalReserveCalculator = new TechnicalReserveCalculator(technicalBasis.Item1, technicalBasis.Item2, policies);
      technicalReserveCalculator.Calculate();

      // Example of use
      var value1 = marketBasis[Gender.Male][State.Active][State.Disabled](30, 4);
      var value2 = marketBasis[Gender.Male][State.Active][State.Dead](30, 4);
      var value3 = marketBasis[Gender.Male][State.Active][State.FreePolicyActive](30, 4);
      var value4 = marketBasis[Gender.Male][State.Active][State.Surrender](30, 4);

      var sum = value1 + value2 + value3 + value4;
      var muDot = marketBasis[Gender.Male][State.Active][State.Active](30, 4);

      Console.WriteLine("Hello World! Calculated muDot is {0} and manual sum is {1}", muDot, sum);

      stopWatch.Stop();
      var timeInSeconds = stopWatch.ElapsedMilliseconds / 1000;
      Console.WriteLine("Time in seconds for program: " + timeInSeconds.ToString());
    }
  }
}
