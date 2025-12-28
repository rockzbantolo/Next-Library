using Next.Clients.Core;
using System.Threading.Tasks;

namespace Next.Clients.Backup
{
    internal interface IBackup
    {
        Task<bool> DumpAsync(Base db, string filePath);
    }
}
