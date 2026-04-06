using MeAiUtility.MultiProvider.Exceptions;

namespace MeAiUtility.MultiProvider.Options;

public static class CopilotOptionGuards
{
    public static void ThrowIfCopilotOnlyOptionsSpecified(ConversationExecutionOptions? execution, string providerName)
    {
        if (execution is null)
        {
            return;
        }

        if (execution.Attachments is not null)
        {
            throw new Exceptions.NotSupportedException("Attachments are only supported by GitHubCopilot provider.", providerName, "Attachments");
        }

        if (execution.SkillDirectories is not null)
        {
            throw new Exceptions.NotSupportedException("SkillDirectories are only supported by GitHubCopilot provider.", providerName, "SkillDirectories");
        }

        if (execution.DisabledSkills is not null)
        {
            throw new Exceptions.NotSupportedException("DisabledSkills are only supported by GitHubCopilot provider.", providerName, "DisabledSkills");
        }
    }
}
