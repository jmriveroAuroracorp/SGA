package com.example.sga.data.model.conteos

enum class EstadoOrden {
    PLANIFICADO,
    ASIGNADO,
    EN_PROCESO,
    PENDIENTE_REVISION,
    CERRADO,
    CANCELADO
}

enum class Visibilidad {
    VISUAL,
    OCULTO
}

enum class ModoGeneracion {
    MANUAL,
    AUTO
}

enum class Alcance {
    ARTICULO,
    UBICACION,
    PALET,
    ESTANTERIA,
    PASILLO,
    ALMACEN
}

enum class AccionFinal {
    SUPERVISION,
    APROBADO,
    RECHAZADO,
    AJUSTADO
}

enum class EstadoEscaneoConteo {
    Inactivo,
    EsperandoUbicacion,
    EsperandoArticulo,
    EsperandoCantidad,
    Procesando
}
