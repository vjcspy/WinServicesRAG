using Microsoft.Extensions.Logging;
using WinServicesRAG.Core.Processing;
namespace WinServicesRAG.Core.Observer;

public class JobResultObserver(ILogger logger) : IObserver<JobProcessingResult>
{

    public void OnNext(JobProcessingResult result)
    {
        logger.LogInformation(">>>>>>>>> Job {JobId} processed: Success={Success}, Message={Message}",
            result.JobName, result.Success, result.Message);
    }

    public void OnError(Exception error)
    {
        logger.LogError(error, ">>>>>>>>> Error in job processing results stream");
    }

    public void OnCompleted()
    {
        logger.LogInformation(">>>>>>>>> Job processing results stream completed");
    }
}
