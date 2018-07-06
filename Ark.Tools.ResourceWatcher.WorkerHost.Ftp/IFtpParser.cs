namespace Ark.Tools.ResourceWatcher.WorkerHost.Ftp
{
    public interface IFtpParser<TPayload>
    {
        TPayload Parse(FtpMetadata metadata, byte[] contents);
    }
}
