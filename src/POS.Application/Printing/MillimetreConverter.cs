namespace POS.Application.Printing;

public static class MillimetreConverter
{
    public const double DipPerInch = 96d;
    public const double MillimetresPerInch = 25.4d;

    public static double ToDip(double millimetres) =>
        millimetres * DipPerInch / MillimetresPerInch;

    public static double ToMillimetres(double dip) =>
        dip * MillimetresPerInch / DipPerInch;
}
