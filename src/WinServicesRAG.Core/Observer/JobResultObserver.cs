using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Processing;
namespace WinServicesRAG.Core.Observer;

public class JobResultObserver(ILogger logger) : IObserver<JobProcessingResult>
{

    public void OnNext(JobProcessingResult result)
    {
        logger.LogInformation(message: "Job {JobId} processed: Success={Success}, Error={Error}",
            result.JobId, result.Success, result.ErrorMessage);
    }

    public void OnError(Exception error)
    {
        logger.LogError(exception: error, message: "Error in job processing results stream");
    }

    public void OnCompleted()
    {
        logger.LogInformation(message: "Job processing results stream completed");
    }
}
