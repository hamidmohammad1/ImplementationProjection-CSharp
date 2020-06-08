using System;
using System.Collections.Generic;
using System.Linq;

using static ProjectionSemiMarkov.HelperFunctions;

namespace ProjectionSemiMarkov
{
  public class ProjectionInput
  {
    /// <summary>
    /// The portfolio wide original technical reserves indexed per policy
    /// </summary>
    public Dictionary<string, double[]> PortfolioWideOriginalTechReserves { get; set; }

    /// <summary>
    /// The portfolio wide bonus technical reserves indexed per policy
    /// </summary>
    public Dictionary<string, double[]> PortfolioWideBonusTechReserves { get; set; }

    /// <summary>
    /// The technical bonus reserves indexed per policy and per standard state.
    /// </summary>
    public Dictionary<string, Dictionary<State, double[]>> BonusTechnicalReserves { get; set; }

    /// <summary>
    /// The market cash flows for original payments per policy id.
    /// </summary>
    public Dictionary<string, double[]> MarketOriginalCashFlows { get; set; }

    /// <summary>
    /// The market cash flows for bonus payments per policy id.
    /// </summary>
    public Dictionary<string, double[]> MarketBonusCashFlows { get; set; }

    /// <summary>
    /// The probabilities indexed per policy and per standard state at time zero with initial state and initial duration.
    /// </summary>
    public Dictionary<string, Dictionary<State, double[][]>> ProbabilitiesTimeZero { get; set; }

    /// <summary>
    /// The rho probabilities indexed per policy and per standard state at time zero with initial state and initial duration.
    /// </summary>
    public Dictionary<string, Dictionary<State, double[][]>> RhoProbabilitiesTimeZero { get; set; }

    /// <summary>
    /// The policies.
    /// </summary>
    public Dictionary<string, Policy> Policies { get; set; }

    /// <summary>
    /// The step size used for calculations.
    /// </summary>
    public double StepSize { get; set; }

    public ProjectionInput()
    {
      var technicalReserveCalculator = new TechnicalReserveCalculator();
      technicalReserveCalculator.Calculate();

      var (originalTechReserves, originalTechPositiveReserves, bonusTechnicalReserves)
        = technicalReserveCalculator.CalculateTechnicalReserve();

      var marketProbabilityCalculator = new ProbabilityCalculator(
          time: 0,
          freePolicyFactor: technicalReserveCalculator.CalculateFreePolicyFactor(),
          originalTechReserves: originalTechReserves,
          originalPositiveTechReserves: originalTechPositiveReserves,
          bonusTechReserves: bonusTechnicalReserves);

      // Initial state must be active or disability, if one wants to calculate RhoModifiedProbabilities
      marketProbabilityCalculator.Calculate(calculateRhoProbability: true);

      Policies = marketProbabilityCalculator.policies;
      BonusTechnicalReserves = bonusTechnicalReserves;

      RhoProbabilitiesTimeZero = marketProbabilityCalculator.RhoProbabilities;
      ProbabilitiesTimeZero = marketProbabilityCalculator.Probabilities;

      PortfolioWideOriginalTechReserves =
        CalculatePortfolioWideOriginalTechReserves(originalTechReserves, originalTechPositiveReserves);
      PortfolioWideBonusTechReserves = CalculatePortfolioWideBonusTechReserves(BonusTechnicalReserves);

      MarketOriginalCashFlows = marketProbabilityCalculator.CalculateMarketOriginalCashFlows();
      MarketBonusCashFlows = marketProbabilityCalculator.CalculateMarketBonusCashFlows();

      StepSize = marketProbabilityCalculator.stepSize;
    }

    /// <summary>
    /// Calculating portfolio wide original technical reserves
    /// </summary>
    /// <remarks>
    /// Since technical basis do not have duration, we have that
    ///  \bar{V}^{*,\circ,g}(t)
    ///  = \sum_{j\in \mathcal{J}^p\setminus \{J\}}V_{j}^{*,\circ}(t)p_{z_0j}(0,t,u_0,\infty)
    ///     +\sum_{j\in \mathcal{J}^f\setminus \{2J+1\}}V_{j'}^{*,\circ, +}(t)p^\rho_{z_0j}(0,t,u_0,\infty)
    /// Since surrender and death states do have zero reserves, we only sum over other states
    /// </remarks>
    private Dictionary<string, double[]> CalculatePortfolioWideOriginalTechReserves(
      Dictionary<string, Dictionary<State, List<double>>> originalTechReserves,
      Dictionary<string, Dictionary<State, List<double>>> originalTechPositiveReserves)
    {
      var standardStates = GiveCollectionOfStates(StateCollection.Standard);
      var freePolicyStates = GiveCollectionOfStates(StateCollection.FreePolicyStates);

      var sumOverStandardStates =
        originalTechReserves.ToDictionary(x => x.Key, x => Enumerable.Range(0, x.Value.First().Value.Count)
          .Select(timePoint => standardStates
            .Sum(state => x.Value[state][timePoint] * ProbabilitiesTimeZero[x.Key][state][timePoint].Last())));

      var sumOverFreePolicyStates =
        originalTechPositiveReserves.ToDictionary(x => x.Key, x => Enumerable.Range(0, x.Value.First().Value.Count)
          .Select(timePoint => freePolicyStates
            .Sum(state => x.Value[ConvertToStandardState(state)][timePoint] * RhoProbabilitiesTimeZero[x.Key][state][timePoint].Last())));

      return sumOverStandardStates.ToDictionary(
        x => x.Key,
        x => x.Value.Zip(sumOverFreePolicyStates[x.Key], (z, y) => z + y).ToArray());
    }

    /// <summary>
    /// Calculating portfolio wide bonus technical reserves
    /// </summary>
    /// <remarks>
    /// Since technical basis do not have duration, we have that
    ///  \bar{V}^{*,\dagger,g}(t)
    ///  = \sum_{j\in \mathcal{J}^p\setminus \{J,2J+1\}}V_{j'}^{*,\circ}(t)p_{z_0j}(0,t,u_0,\infty)
    /// Since surrender and death states do have zero reserves, we only sum over other states
    /// </remarks>
    private Dictionary<string, double[]> CalculatePortfolioWideBonusTechReserves(
      Dictionary<string, Dictionary<State, double[]>> bonusTechReserves)
    {
      var allStatesButSurrender = GiveCollectionOfStates(StateCollection.AllStates)
        .Where(x => x != State.FreePolicySurrender && x != State.Surrender);

      return bonusTechReserves.ToDictionary(x => x.Key, x => Enumerable.Range(0, x.Value.First().Value.Length)
        .Select(timePoint => allStatesButSurrender
          .Sum(state => x.Value[ConvertToStandardState(state)][timePoint] * ProbabilitiesTimeZero[x.Key][state][timePoint].Last()))
        .ToArray());
    }
  }
}
