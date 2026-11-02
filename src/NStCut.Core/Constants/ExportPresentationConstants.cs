namespace NStCut.Core.Constants;

public static class ExportPresentationConstants
{
    public const double SheetSpacing = 25.0;
    public const double LabelOffset = 10.0;
    public const double LabelFontHeight = 5.0;
    public const double ArrowClearance = 5.0;
    public const double ArrowBaselineOffset = LabelOffset + LabelFontHeight + ArrowClearance;

    public const double MaxArrowHalfLength = 40.0;
    public const double ArrowHeadLength = 5.0;
    public const double ArrowHeadAngle = Math.PI / 6.0;
    public const double GrainArrowLineThickness = 2.0;

    public const double AnnotationClearance = 5.0;
    public const double AnnotationBandHeight = ArrowBaselineOffset + 2 * MaxArrowHalfLength + AnnotationClearance;

    public const int MaxReportThumbnailWidth = 400;
    public const int MaxReportThumbnailHeight = 300;
}
