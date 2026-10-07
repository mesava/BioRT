using BioRT.Core.Analysis;
using BioRT.Core.Models;
using BioRT.IO.Dicom;
using FellowOakDicom;
using FellowOakDicom.Imaging;
using FellowOakDicom.IO.Buffer;
using Xunit;

namespace BioRT.Tests;

public class SyntheticDicomCommissioningTests
{
    [Fact]
    public async Task FolderAndStreamImportPaths_ProduceSameAnalysisFingerprint()
    {
        byte[] planBytes = SaveToBytes(CreateRtPlan());
        byte[] doseBytes = SaveToBytes(CreateRtDose());
        byte[] structBytes = SaveToBytes(CreateRtStruct());

        string temp = Path.Combine(
            Path.GetTempPath(),
            "BioRT_" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(temp);

        try
        {
            string planPath = Path.Combine(temp, "RTPLAN.dcm");
            string dosePath = Path.Combine(temp, "RTDOSE.dcm");
            string structPath = Path.Combine(temp, "RTSTRUCT.dcm");

            await File.WriteAllBytesAsync(planPath, planBytes);
            await File.WriteAllBytesAsync(dosePath, doseBytes);
            await File.WriteAllBytesAsync(structPath, structBytes);

            // Console-equivalent folder path.
            PlanData folderPlan =
                new DicomImporter().Load(temp);

            var structReader = new RtStructReader();
            var folderStruct = DicomFile.Open(structPath);

            IReadOnlyDictionary<int, string> folderNames =
                structReader.ReadStructureNames(folderStruct);

            IReadOnlyDictionary<int, List<double[]>> folderContours =
                structReader.ReadContours(folderStruct);

            // Web-equivalent stream path.
            DicomImportResult streamImport =
                await new DicomBundleImporter().LoadAsync(
                    new[]
                    {
                        FromBytes("RTPLAN.dcm", planBytes),
                        FromBytes("RTDOSE.dcm", doseBytes),
                        FromBytes("RTSTRUCT.dcm", structBytes)
                    });

            IReadOnlyList<DoseCriterion> criteria = CreateCriteria();

            PlanAnalysisResult folderAnalysis =
                new PlanAnalysisService().Analyze(
                    new PlanAnalysisRequest
                    {
                        Plan = folderPlan,
                        StructureNames = folderNames,
                        Contours = folderContours,
                        Criteria = criteria
                    });

            PlanAnalysisResult streamAnalysis =
                new PlanAnalysisService().Analyze(
                    new PlanAnalysisRequest
                    {
                        Plan = streamImport.Plan,
                        StructureNames = streamImport.StructureNames,
                        Contours = streamImport.Contours,
                        Criteria = criteria
                    });

            string folderFingerprint =
                PlanAnalysisFingerprint.Compute(
                    folderPlan,
                    folderAnalysis);

            string streamFingerprint =
                PlanAnalysisFingerprint.Compute(
                    streamImport.Plan,
                    streamAnalysis);

            Assert.Equal(folderFingerprint, streamFingerprint);

            PlanAnalysisComparisonResult toleranceComparison =
                PlanAnalysisComparator.Compare(
                    folderPlan,
                    folderAnalysis,
                    streamImport.Plan,
                    streamAnalysis);

            Assert.True(
                toleranceComparison.IsMatch,
                string.Join(
                    Environment.NewLine,
                    toleranceComparison.Differences.Select(
                        d => $"{d.Path}: {d.Baseline} vs {d.Candidate} ({d.Rule})")));

            Assert.Equal(30, folderPlan.Fractions);
            Assert.Equal(2.0, folderPlan.DosePerFraction, precision: 12);

            PtvAnalysisResult folderPtv =
                Assert.Single(folderAnalysis.PtvMetrics);

            Assert.Equal("PTV_60", folderPtv.MatchedStructureName);
            Assert.Equal(60.0, folderPtv.D50Gy, precision: 12);

            ClinicalCriterionEvaluation oar =
                Assert.Single(folderAnalysis.ClinicalCriteria);

            Assert.Equal("OAR", oar.MatchedStructureName);
            Assert.Equal(60.0, oar.Value, precision: 12);
            Assert.True(oar.Pass);
        }
        finally
        {
            if (Directory.Exists(temp))
                Directory.Delete(temp, recursive: true);
        }
    }

    private static IReadOnlyList<DoseCriterion> CreateCriteria()
        =>
        [
            new DoseCriterion
            {
                StructureName = "PTV_60",
                Type = CriterionType.DxxPercent,
                DxPercent = 50.0,
                Operator = ">=",
                Limit = 60.0,
                Raw = "D50% >= 60 Gy"
            },
            new DoseCriterion
            {
                StructureName = "PTV_60",
                Type = CriterionType.DxxPercent,
                DxPercent = 95.0,
                Operator = ">=",
                Limit = 57.0,
                Raw = "D95% >= 57 Gy"
            },
            new DoseCriterion
            {
                StructureName = "OAR",
                Type = CriterionType.Dmean,
                Operator = "<=",
                Limit = 65.0,
                Raw = "Dmean <= 65 Gy"
            }
        ];

    private static DicomInputFile FromBytes(
        string name,
        byte[] bytes)
        => new()
        {
            Name = name,
            OpenReadAsync = _ =>
                ValueTask.FromResult<Stream>(
                    new MemoryStream(
                        bytes,
                        writable: false))
        };

    private static byte[] SaveToBytes(DicomFile file)
    {
        using var stream = new MemoryStream();
        file.Save(stream);
        return stream.ToArray();
    }

    private static DicomFile CreateRtPlan()
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

        var dataset =
            new DicomDataset(
                DicomTransferSyntax.ExplicitVRLittleEndian);

        dataset.Add(DicomTag.PatientID, "SYNTHETIC_COMMISSIONING");
        dataset.Add(DicomTag.Modality, "RTPLAN");
        dataset.Add(DicomTag.SOPClassUID, DicomUID.RTPlanStorage);
        dataset.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());

        dataset.Add(
            new DicomSequence(
                DicomTag.FractionGroupSequence,
                fractionGroup));

        dataset.Add(
            new DicomSequence(
                DicomTag.DoseReferenceSequence,
                doseReference));

        return new DicomFile(dataset);
    }

    private static DicomFile CreateRtDose()
    {
        var dataset =
            new DicomDataset(
                DicomTransferSyntax.ExplicitVRLittleEndian);

        dataset.Add(DicomTag.PatientID, "SYNTHETIC_COMMISSIONING");
        dataset.Add(DicomTag.Modality, "RTDOSE");
        dataset.Add(DicomTag.SOPClassUID, DicomUID.RTDoseStorage);
        dataset.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());

        dataset.Add(DicomTag.DoseUnits, "GY");
        dataset.Add(DicomTag.DoseType, "PHYSICAL");
        dataset.Add(DicomTag.DoseSummationType, "PLAN");
        dataset.Add(DicomTag.DoseGridScaling, 0.01);

        dataset.Add(DicomTag.Rows, (ushort)2);
        dataset.Add(DicomTag.Columns, (ushort)2);
        dataset.Add(DicomTag.PixelSpacing, 1.0, 1.0);
        dataset.Add(DicomTag.ImagePositionPatient, 0.0, 0.0, 0.0);
        dataset.Add(DicomTag.GridFrameOffsetVector, 0.0);

        dataset.Add(DicomTag.SamplesPerPixel, (ushort)1);
        dataset.Add(
            DicomTag.PhotometricInterpretation,
            PhotometricInterpretation.Monochrome2.Value);
        dataset.Add(DicomTag.BitsAllocated, (ushort)16);
        dataset.Add(DicomTag.BitsStored, (ushort)16);
        dataset.Add(DicomTag.HighBit, (ushort)15);
        dataset.Add(DicomTag.PixelRepresentation, (ushort)0);

        DicomPixelData pixelData =
            DicomPixelData.Create(dataset, true);

        ushort rawDose = 6000;
        byte[] frame = new byte[2 * 2 * sizeof(ushort)];

        for (int i = 0; i < 4; i++)
        {
            byte[] value = BitConverter.GetBytes(rawDose);
            frame[2 * i] = value[0];
            frame[2 * i + 1] = value[1];
        }

        pixelData.AddFrame(
            new MemoryByteBuffer(frame));

        return new DicomFile(dataset);
    }

    private static DicomFile CreateRtStruct()
    {
        double[] square =
        [
            -0.1, -0.1, 0.0,
             2.1, -0.1, 0.0,
             2.1,  2.1, 0.0,
            -0.1,  2.1, 0.0
        ];

        var ptvRoi = new DicomDataset();
        ptvRoi.Add(DicomTag.ROINumber, 1);
        ptvRoi.Add(DicomTag.ROIName, "PTV_60");

        var oarRoi = new DicomDataset();
        oarRoi.Add(DicomTag.ROINumber, 2);
        oarRoi.Add(DicomTag.ROIName, "OAR");

        var ptvContour = new DicomDataset();
        ptvContour.Add(DicomTag.ContourGeometricType, "CLOSED_PLANAR");
        ptvContour.Add(DicomTag.NumberOfContourPoints, 4);
        ptvContour.Add(DicomTag.ContourData, square);

        var oarContour = new DicomDataset();
        oarContour.Add(DicomTag.ContourGeometricType, "CLOSED_PLANAR");
        oarContour.Add(DicomTag.NumberOfContourPoints, 4);
        oarContour.Add(DicomTag.ContourData, square);

        var ptvRoiContour = new DicomDataset();
        ptvRoiContour.Add(DicomTag.ReferencedROINumber, 1);
        ptvRoiContour.Add(
            new DicomSequence(
                DicomTag.ContourSequence,
                ptvContour));

        var oarRoiContour = new DicomDataset();
        oarRoiContour.Add(DicomTag.ReferencedROINumber, 2);
        oarRoiContour.Add(
            new DicomSequence(
                DicomTag.ContourSequence,
                oarContour));

        var dataset =
            new DicomDataset(
                DicomTransferSyntax.ExplicitVRLittleEndian);

        dataset.Add(DicomTag.PatientID, "SYNTHETIC_COMMISSIONING");
        dataset.Add(DicomTag.Modality, "RTSTRUCT");
        dataset.Add(DicomTag.SOPClassUID, DicomUID.RTStructureSetStorage);
        dataset.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());

        dataset.Add(
            new DicomSequence(
                DicomTag.StructureSetROISequence,
                ptvRoi,
                oarRoi));

        dataset.Add(
            new DicomSequence(
                DicomTag.ROIContourSequence,
                ptvRoiContour,
                oarRoiContour));

        return new DicomFile(dataset);
    }
}
