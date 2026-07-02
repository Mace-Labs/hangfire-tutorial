namespace GamingSeeds.Hangfire.Demo.Jobs;

public interface IImageProcessingJobs
{
    Task ProcessImage(int imageId);
    Task GenerateThumbnails(int imageId);
    Task OptimizeImages(int imageId);
}
