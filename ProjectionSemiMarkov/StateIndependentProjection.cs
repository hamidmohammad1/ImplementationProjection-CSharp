using System;
using System.Collections.Generic;
using System.Text;

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
    /// The step size for projection.
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
      //TODO Initialize
      //TODO Make a reserve-calculator based on zero coupon and cash flow
      //TODO Make a time step from underlying prob/cash flow to projection time converter.
      // [0, 1, 2, 3, ...]
      // [0, 1/12, 2/12, 3/12, ...]

      ecoResult.PortfolioWideTechnicalReserve[0] = 0.0;
      ecoResult.PortfolioWideMarketReserve[0] = Input.PortfolioWideOriginalTechReserves["policy1"][0];
      foreach (var keyValuePair in ecoResult.DividendProcess)
        keyValuePair.Value[0] = 0.0;
      foreach (var keyValuePair in ecoResult.TransactionProcess)
        keyValuePair.Value[0] = 0.0;
      foreach (var keyValuePair in ecoResult.InvestmentPortfolioEquity)
        keyValuePair.Value[0] = 0.0;
      foreach (var keyValuePair in ecoResult.InvestmentPortfolioAssetProcess)
        keyValuePair.Value[0] = 0.0;
      ecoResult.QProcess[0] = 0.0;
      ecoResult.Equity[0] = 0.0;
      ecoResult.AssetProcess[0] = 0.0;
      ecoResult.ProjectedBonusCashFlow[0] = 0.0;
    }

    /// <summary>
    /// Projecting for all economic scenarios.
    /// </summary>
    public void Project()
    {
      for (var i = 0; i < NumberOfEconomicScenarios; i++)
      {
        var ecoResult = new StateIndependentProjectionResult(
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

    public class StateIndependentProjectionResult
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
      /// The transaction process, \xi^0(t) and \xi^1(t)
      /// </summary>
      public Dictionary<Index, double[]> TransactionProcess { get; set; }

      /// <summary>
      /// Investment portfolio for equity, \eta^E(t)
      /// </summary>
      public Dictionary<Assets, double[]> InvestmentPortfolioEquity { get; set; }

      /// <summary>
      /// Investment portfolio for asset process, \eta^U(t)
      /// </summary>
      public Dictionary<Assets, double[]> InvestmentPortfolioAssetProcess { get; set; }

      /// <summary>
      /// The Q-process, Q(t)
      /// </summary>
      public double[] QProcess { get; set; }

      /// <summary>
      /// The equity, E(t)
      /// </summary>
      public double[] Equity { get; set; }

      /// <summary>
      /// The asset process, U_S(t)
      /// </summary>
      public double[] AssetProcess { get; set; }

      /// <summary>
      /// The projection bonus cash flow, A^b(0, ds)
      /// </summary>
      public double[] ProjectedBonusCashFlow { get; set; }

      /// <summary>
      /// The economic scenario, S_0(t), S_1(t)
      /// </summary>
      public Dictionary<Assets, double[]> EconomicScenario { get; set; }

      public StateIndependentProjectionResult(
        int numberOfProjectionTimes,
        Dictionary<Assets, double[]> economicScenario)
      {
        PortfolioWideTechnicalReserve = new double[numberOfProjectionTimes];
        PortfolioWideMarketReserve = new double[numberOfProjectionTimes];
        QProcess = new double[numberOfProjectionTimes];
        Equity = new double[numberOfProjectionTimes];
        AssetProcess = new double[numberOfProjectionTimes];
        ProjectedBonusCashFlow = new double[numberOfProjectionTimes];
        EconomicScenario = economicScenario;

        DividendProcess = new Dictionary<Index, double[]>
          {
            { Index.Zero, new double[numberOfProjectionTimes] },
            { Index.One, new double[numberOfProjectionTimes] },
          };
        TransactionProcess = new Dictionary<Index, double[]>
          {
            { Index.Zero, new double[numberOfProjectionTimes] },
            { Index.One, new double[numberOfProjectionTimes] },
          };
        InvestmentPortfolioEquity = new Dictionary<Assets, double[]>
          {
            { Assets.ShortRate, new double[numberOfProjectionTimes] },
            { Assets.RiskyAsset, new double[numberOfProjectionTimes] },
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
