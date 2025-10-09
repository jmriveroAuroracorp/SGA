using SGA_Api.Models.RolesSga;

namespace SGA_Api.Services
{
    public interface IRolesSgaService
    {
        Task<IEnumerable<RolSgaDto>> GetRolesSgaAsync();
        Task<RolSgaDto?> GetRolSgaByIdAsync(int id);
        Task<RolSugeridoDto?> SuggestRolSgaAsync(int operarioId);
        Task<IEnumerable<RolSgaDto>> ObtenerRolesSgaAsync();
        Task<RolSgaDto?> ObtenerRolSgaPorIdAsync(int id);
        Task<RolSugeridoDto> ObtenerRolSugeridoAsync(int operarioId);
    }
}
