using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Math = System.Math;

namespace ProjectionSemiMarkov
{
  class StateIndependentProjection
  {
    /// <summary>
    /// The projection Input
    /// </summary>
    public ProjectionInput Input { get; private set; }

    /// <summary>
    /// The economic scenario generator Input
    /// </summary>
    public EconomicScenarioGenerator EcoScenarioGenerator { get; private set; }

    /// <summary>
    /// The number of economic scenarios.
    /// </summary>
    public int NumberOfEconomicScenarios { get; private set; }

    /// <summary>
    /// The step size for projection. It must a multiple of <see cref="ProjectionInput.StepSize"/>.
    /// </summary>
    public double ProjectionStepSize = 1;

    /// <summary>
    /// The end time.
    /// </summary>
    public double ProjectionEndTime => EcoScenarioGenerator.LastExpiryTime;

    /// <summary>
    /// The end time.
    /// </summary>
    public int NumberOfProjectionTimes => (int)(ProjectionEndTime / ProjectionStepSize + 1);

    /// <summary>
    /// The projection result indexed on economic scenario.
    /// </summary>
    public List<StateIndependentProjectionResult> ProjectionResult { get; private set; }

    /// <summary>
    /// Constructs a instance of StateIndependentProjection.
    /// </summary>
    public StateIndependentProjection(
      ProjectionInput input,
      EconomicScenarioGenerator ecoScenarioGenerator,
      int numberOfEconomicScenarios)
    {
      Input = input;
      EcoScenarioGenerator = ecoScenarioGenerator;
      NumberOfEconomicScenarios = numberOfEconomicScenarios;
      ProjectionResult = new List<StateIndependentProjectionResult>();
    }

    /// <summary>
    /// Initializing StateIndependentProjectionResult for time zero.
    /// </summary>
    public void Initialize(StateIndependentProjectionResult ecoResult)
    {
      // Portfolio quantities
      foreach (var portfolio in ecoResult.PortfolioResults)
      {
        portfolio.Value.AssetProcess[0] = Math.Pow(10, 8);
        portfolio.Value.DividendProcess[Index.Zero][0] = 0.0;
        portfolio.Value.DividendProcess[Index.One][0] = 0.0;
        portfolio.Value.InvestmentPortfolioAssetProcess[Assets.ShortRate][0] = 0.0;
        portfolio.Value.InvestmentPortfolioAssetProcess[Assets.RiskyAsset][0] = 0.0;

        portfolio.Value.ProjectedBonusCashFlow[0] = 0.0;
        portfolio.Value.TransactionProcess[Index.Zero][0] = 0.0;
        portfolio.Value.TransactionProcess[Index.One][0] = 0.0;

        var initialQValue = 0.0;
        portfolio.Value.QProcess[0] = initialQValue;

        // Setting initial portfolio wide mean reserve at time zero
        portfolio.Value.PortfolioWideMarketReserve[0] = CalculatePortFolioWideMarketReservePerPortfolio(0,
          ecoResult.EconomicScenario[Assets.ShortRate][0], portfolio.Key, initialQValue);
        portfolio.Value.PortfolioWideTechnicalReserve[0] = Input.PortfolioWideOriginalTechReserves[portfolio.Key][0]
          + initialQValue * Input.PortfolioWideBonusTechReserves[portfolio.Key][0];
      }

      // Equity
      ecoResult.EquityResults.Equity[0] = Math.Pow(10, 7);
      foreach (var keyValuePair in ecoResult.EquityResults.InvestmentPortfolioEquity)
        keyValuePair.Value[0] = 0.0;
    }

    /// <summary>
    /// Calculating reserve by discounting cash flow with relevant zero coupon bond prices.
    /// </summary>
    public double CalculatePortFolioWideMarketReservePerPortfolio(int timePoint, double shortRateAtTimePoint,
      string portfolioId, double qValue)
    {
      return ReserveCalculator(
        timePoint,
        shortRateAtTimePoint,
        Input.MarketOriginalCashFlows[portfolioId]
          .Zip(Input.MarketBonusCashFlows[portfolioId], (x, y) => x + qValue * y).ToArray());
    }

    /// <summary>
    /// Calculating reserve by discounting cash flow with relevant zero coupon bond prices.
    /// </summary>
    public double ReserveCalculator(int timePoint, double shortRate, double[] cashFlows)
    {
      var reserve = 0.0;

      // We minus with one, cause we are integrating over intervals (t_{i}, t_{i+1}], ..., (t_{n-1}, t_{n}].
      for (var i = timePoint; i < ProjectionIndexCalculatorIndexConverter(cashFlows.Length, true) - 1; i++)
      {
        reserve += EcoScenarioGenerator.ZeroCouponBondPrices(shortRate, ProjectionIndexToTimeInYear(timePoint),
            ProjectionIndexToTimeInYear(i + 0.5))
          * (cashFlows[ProjectionIndexCalculatorIndexConverter(i + 1)] - cashFlows[ProjectionIndexCalculatorIndexConverter(i)])
          * 0.5 * ProjectionStepSize;
      }

      return reserve;
    }

    /// <summary>
    /// Projecting for all economic scenarios.
    /// </summary>
    public void Project()
    {
      for (var i = 0; i < NumberOfEconomicScenarios; i++)
      {
        var ecoResult = new StateIndependentProjectionResult(
          Input.Policies.Keys,
          NumberOfProjectionTimes,
          EcoScenarioGenerator.SimulateMarket());

        ProjectPerEconomicScenario(ecoResult);

        ProjectionResult.Add(ecoResult);
      }
    }

    /// <summary>
    /// Projecting for per economic scenarios.
    /// </summary>
    public void ProjectPerEconomicScenario(StateIndependentProjectionResult ecoResult)
    {
      Initialize(ecoResult);

      for (var i = 1; i < NumberOfProjectionTimes; i++)
        ProjectPerTimePoint(i, ecoResult);
    }

    /// <summary>
    /// Projecting for per time point.
    /// </summary>
    public void ProjectPerTimePoint(int index, StateIndependentProjectionResult ecoResult)
    {
      //TODO Calculate mean portfolio

      //TODO Calculate controls

      //TODO Project Q, E, Us

      //TODO Calculate next time bonus cash flow
    }

    /// <summary>
    /// Converts a time point in unit projection step size into units of step size from calculator or reverse
    /// based on a boolean.
    /// </summary>
    public int ProjectionIndexCalculatorIndexConverter(int timePoint, bool reverse = false)
    {
      if (reverse)
        return (int)(timePoint * Input.StepSize);

      return (int)(timePoint / Input.StepSize);
    }

    /// <summary>
    /// Converts a time point in unit projection step size to time in year
    /// </summary>
    public int ProjectionIndexToTimeInYear(double timePoint)
    {
      return (int)(timePoint * ProjectionStepSize);
    }

    public class StateIndependentProjectionResult
    {
      /// <summary>
      /// The results per portfolio
      /// </summary>
      public Dictionary<string, PortfolioResult> PortfolioResults { get; set; }

      /// <summary>
      /// The results for equity
      /// </summary>
      public EquityResult EquityResults { get; set; }

      /// <summary>
      /// The economic scenario, S_0(t), S_1(t)
      /// </summary>
      public Dictionary<Assets, double[]> EconomicScenario { get; set; }

      public StateIndependentProjectionResult(
        IEnumerable<string> policyIds,
        int numberOfProjectionTimes,
        Dictionary<Assets, double[]> economicScenario)
      {
        PortfolioResults = policyIds.ToDictionary(x => x, x => new PortfolioResult(numberOfProjectionTimes));
        EconomicScenario = economicScenario;
        EquityResults = new EquityResult(numberOfProjectionTimes);
      }
    }

    public class EquityResult
    {
      /// <summary>
      /// The equity, E(t)
      /// </summary>
      public double[] Equity { get; set; }

      /// <summary>
      /// Investment portfolio for equity, \eta^E(t)
      /// </summary>
      public Dictionary<Assets, double[]> InvestmentPortfolioEquity { get; set; }

      public EquityResult(int numberOfProjectionTimes)
      {
        Equity = new double[numberOfProjectionTimes];

        InvestmentPortfolioEquity = new Dictionary<Assets, double[]>
          {
            { Assets.ShortRate, new double[numberOfProjectionTimes] },
            { Assets.RiskyAsset, new double[numberOfProjectionTimes] },
          };
      }
    }

    public class PortfolioResult
    {
      /// <summary>
      /// The portfolio wide technical reserves (Savings account, X(t))
      /// </summary>
      public double[] PortfolioWideTechnicalReserve { get; set; }

      /// <summary>
      /// The portfolio wide market reserves (Guaranteed benefits, GY(t))
      /// </summary>
      public double[] PortfolioWideMarketReserve { get; set; }

      /// <summary>
      /// The dividend process, \widetilde{\delta}^0(t) and \widetilde{\delta}^1(t)
      /// </summary>
      public Dictionary<Index, double[]> DividendProcess { get; set; }

      /// <summary>
      /// The Q-process, Q(t) per policy
      /// </summary>
      public double[] QProcess { get; set; }

      /// <summary>
      /// The projection bonus cash flow, A^b(0, ds)
      /// </summary>
      public double[] ProjectedBonusCashFlow { get; set; }

      /// <summary>
      /// The asset process, U_S(t)
      /// </summary>
      public double[] AssetProcess { get; set; }

      /// <summary>
      /// Investment portfolio for asset process, \eta^U(t)
      /// </summary>
      public Dictionary<Assets, double[]> InvestmentPortfolioAssetProcess { get; set; }

      /// <summary>
      /// The transaction process, \xi^0(t) and \xi^1(t)
      /// </summary>
      public Dictionary<Index, double[]> TransactionProcess { get; set; }

      public PortfolioResult(int numberOfProjectionTimes)
      {
        PortfolioWideTechnicalReserve = new double[numberOfProjectionTimes];
        PortfolioWideMarketReserve = new double[numberOfProjectionTimes];
        QProcess = new double[numberOfProjectionTimes];
        ProjectedBonusCashFlow = new double[numberOfProjectionTimes];
        DividendProcess = new Dictionary<Index, double[]>
          {
            { Index.Zero, new double[numberOfProjectionTimes] },
            { Index.One, new double[numberOfProjectionTimes] },
          };
        AssetProcess = new double[numberOfProjectionTimes];
        TransactionProcess = new Dictionary<Index, double[]>
          {
            { Index.Zero, new double[numberOfProjectionTimes] },
            { Index.One, new double[numberOfProjectionTimes] },
          };
        InvestmentPortfolioAssetProcess = new Dictionary<Assets, double[]>
          {
            { Assets.ShortRate, new double[numberOfProjectionTimes] },
            { Assets.RiskyAsset, new double[numberOfProjectionTimes] },
          };
      }
    }
  }
}

