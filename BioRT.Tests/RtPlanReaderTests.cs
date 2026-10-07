using BioRT.Core.Models;
using BioRT.IO.Dicom;
using FellowOakDicom;
using Xunit;

namespace BioRT.Tests;

public class RtPlanReaderTests
{
    [Fact]
    public void ReadsTargetPrescriptionAndFractionCount()
    {
        var fractionGroup = new DicomDataset();
        fractionGroup.Add(
            DicomTag.NumberOfFractionsPlanned,
            30);

        var doseReference = new DicomDataset();
        doseReference.Add(
            DicomTag.DoseReferenceType,
            "TARGET");
        doseReference.Add(
            DicomTag.TargetPrescriptionDose,
            60.0);

        var dataset = new DicomDataset();
        dataset.Add(DicomTag.PatientID, "SYNTHETIC");
        dataset.Add(
            new DicomSequence(
                DicomTag.FractionGroupSequence,
                fractionGroup));
        dataset.Add(
            new DicomSequence(
                DicomTag.DoseReferenceSequence,
                doseReference));

        var file = new DicomFile(dataset);
        var plan = new PlanData();

        new RtPlanReader().Read(file, plan);

        Assert.Equal("SYNTHETIC", plan.PatientId);
        Assert.Equal(30, plan.Fractions);
        Assert.Equal(2.0, plan.DosePerFraction, precision: 12);
        Assert.Equal(60.0, plan.TotalDose, precision: 12);
    }

    [Fact]
    public void UsesFirstTargetDoseReference()
    {
        var fractionGroup = new DicomDataset();
        fractionGroup.Add(
            DicomTag.NumberOfFractionsPlanned,
            25);

        var organAtRisk = new DicomDataset();
        organAtRisk.Add(
            DicomTag.DoseReferenceType,
            "ORGAN_AT_RISK");
        organAtRisk.Add(
            DicomTag.TargetPrescriptionDose,
            10.0);

        var target = new DicomDataset();
        target.Add(
            DicomTag.DoseReferenceType,
            "TARGET");
        target.Add(
            DicomTag.TargetPrescriptionDose,
            50.0);

        var dataset = new DicomDataset();
        dataset.Add(
            new DicomSequence(
                DicomTag.FractionGroupSequence,
                fractionGroup));
        dataset.Add(
            new DicomSequence(
                DicomTag.DoseReferenceSequence,
                organAtRisk,
                target));

        var file = new DicomFile(dataset);
        var plan = new PlanData();

        new RtPlanReader().Read(file, plan);

        Assert.Equal(25, plan.Fractions);
        Assert.Equal(2.0, plan.DosePerFraction, precision: 12);
    }
}
