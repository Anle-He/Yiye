namespace Yiye.Models;

public static class ExperienceRules
{
    public static bool HasValidDates(MediaExperience experience) => experience.StartedOn is not { } start
        || experience.CompletedOn is not { } end || end >= start;
    public static void Validate(MediaExperience experience)
    {
        RatingScale.Validate(experience);
        if (!HasValidDates(experience))
            throw new InvalidOperationException("Completion date cannot be earlier than the start date.");
    }
}
