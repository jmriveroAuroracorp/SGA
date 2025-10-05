using System.ComponentModel.DataAnnotations.Schema;

namespace SGA_Api.Models.Login
{
    /// <summary>
    /// Modelo para la tabla VAuxiliarEmpleado de AURORA
    /// </summary>
    [Table("VAuxiliarEmpleado")]
    public class VAuxiliarEmpleado
    {
        public short CodigoEmpresa { get; set; }
        public int CodigoEmpleado { get; set; }
        public string SiglaNacion { get; set; } = string.Empty;
        public string CifDni { get; set; } = string.Empty;
        public string MRH_RazonSocialEmpleado { get; set; } = string.Empty;
        public string Telefono { get; set; } = string.Empty;
        public string Telefono2 { get; set; } = string.Empty;
        public string Telefono3 { get; set; } = string.Empty;
        public string EMail1 { get; set; } = string.Empty;
        public string EMail2 { get; set; } = string.Empty;
        public string EMail3 { get; set; } = string.Empty;
        public string EMailEmpresa1 { get; set; } = string.Empty;
        public string Observaciones { get; set; } = string.Empty;
        public short StatusActivo { get; set; }
        public string Departamento { get; set; } = string.Empty;
        public DateTime? FechaBaja { get; set; }
        public short CodigoContrato { get; set; }
        public string MRH_TipoColectivo { get; set; } = string.Empty;
        public string MRH_TipoCentro { get; set; } = string.Empty;
        public string MRH_Departamento { get; set; } = string.Empty;
        public string MRH_TipoEmpleado { get; set; } = string.Empty;
        public string MRH_TipoPuesto { get; set; } = string.Empty;
        public string TelefonoEmpresa { get; set; } = string.Empty;
        public string TelefonoExt { get; set; } = string.Empty;
        public string IdBizneo { get; set; } = string.Empty;
        public string IdBiostar { get; set; } = string.Empty;
        public string ProvNumSoe { get; set; } = string.Empty;
        public string NivelEstudios { get; set; } = string.Empty;
        public Guid IdEmpleado { get; set; }
        public byte NumeroHijos { get; set; }
        public byte Sexo { get; set; }
        public DateTime? FechaNacimiento { get; set; }
        public byte EstadoCivil { get; set; }
        public string Profesion { get; set; } = string.Empty;
        public byte DiscapacidadContrib { get; set; }
        public string Domicilio { get; set; } = string.Empty;
        public string CodigoPostal { get; set; } = string.Empty;
        public string Municipio { get; set; } = string.Empty;
        public string Provincia { get; set; } = string.Empty;
        public string Nacion { get; set; } = string.Empty;
        public string IBAN { get; set; } = string.Empty;
        public DateTime? FechaAntiguedad { get; set; }
        public string NombreEmpleado { get; set; } = string.Empty;
        public string PrimerApellidoEmpleado { get; set; } = string.Empty;
        public string SegundoApellidoEmpleado { get; set; } = string.Empty;
        public DateTime? FechaSincroBizneo { get; set; }
        public DateTime? FechaAltaBizneo { get; set; }
        public string ErrorSincroBizneo { get; set; } = string.Empty;
        public string MRH_DepartamentoBizneo { get; set; } = string.Empty;
        public string MRH_CentroBizneo { get; set; } = string.Empty;
        public string MRH_PuestoBizneo { get; set; } = string.Empty;
        public short MRH_MotivoBaja { get; set; }
        public short MRH_Discapacidad { get; set; }
        public short MRH_DiscapacidadPor { get; set; }
        public short MRH_ReduccionJornada { get; set; }
        public decimal MRH_HReduccionJornada { get; set; }
        public string MRH_ComentReduccionJornada { get; set; } = string.Empty;
        public short MRH_Teletrabajo { get; set; }
        public string MRH_ComentTeletrabajo { get; set; } = string.Empty;
    }

    /// <summary>
    /// DTO para empleados disponibles para dar de alta en SGA
    /// </summary>
    public class EmpleadoDisponibleDto
    {
        public int CodigoEmpleado { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty; // "Empleado" o "Operario sin permisos"
    }

    /// <summary>
    /// DTO para dar de alta un empleado en SGA
    /// </summary>
    public class DarAltaEmpleadoDto
    {
        public int CodigoEmpleado { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Contraseña { get; set; }
        public string? CodigoCentro { get; set; }
        public List<short> PermisosIniciales { get; set; } = new List<short>();
        public List<short> EmpresasIniciales { get; set; } = new List<short>();
        public List<string> AlmacenesIniciales { get; set; } = new List<string>();
    }
}

