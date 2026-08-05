using System.Threading.Tasks;
using KID.Models;

namespace KID.Services.Files.Interfaces
{
    public interface IEditorSessionService
    {
        Task<EditorSessionData?> LoadAsync();
        Task SaveAsync(EditorSessionData session);
    }
}
