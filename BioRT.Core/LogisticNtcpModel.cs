namespace BioRT.Core.Radiobiology;

public static class LogisticNtcpModel
{
    public static double Calculate(double linearPredictor)
    {
        // Numerically stable logistic function.
        if (linearPredictor >= 0)
        {
            double z = Math.Exp(-linearPredictor);
            return 1.0 / (1.0 + z);
        }

        double exp = Math.Exp(linearPredictor);
        return exp / (1.0 + exp);
    }
}
