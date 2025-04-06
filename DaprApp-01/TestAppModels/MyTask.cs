namespace TestAppModels
{
    public record MyTask(
        string TaskId,
        PatientInfo Patient,
        List<string> Tags,
        int Priority
    );

    public record PatientInfo(
        string Name,
        DateTime DOB,
        double WeightKg
    );
}
