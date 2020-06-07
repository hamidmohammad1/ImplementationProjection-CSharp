using System;
using System.Collections.Generic;
using System.Text;

namespace ProjectionSemiMarkov
{
  class StateIndependentProjection
  {
    public ProjectionInput Input { get; private set; }

    public StateIndependentProjection(ProjectionInput input, string ecoScenario)
    {
      this.Input = input;
      SetEconomicScenario(ecoScenario);
    }

    private void SetEconomicScenario(string ecoScenario)
    {

    }

    public void Project()
    {

    }

    public void ProjectPerEconomicScenario()
    {
    }

    public void ProjectPerTimePoint()
    {
      //TODO Calculate mean portfolio

      //TODO Calculate controls

      //TODO Project Q, E, Us

      //TODO Calculate next time bonus cash flow
    }

    public void Result()
    {
    }
  }
}
