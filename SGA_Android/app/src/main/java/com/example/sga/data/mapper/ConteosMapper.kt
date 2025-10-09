package com.example.sga.data.mapper

import com.example.sga.data.dto.conteos.*
import com.example.sga.data.model.conteos.*

object ConteosMapper {
    
    fun fromOrdenConteoDto(dto: OrdenConteoDto): OrdenConteo {
        return OrdenConteo(
            guidID = dto.guidID,
            codigoEmpresa = dto.codigoEmpresa,
            titulo = dto.titulo,
            visibilidad = dto.visibilidad,
            modoGeneracion = dto.modoGeneracion,
            alcance = dto.alcance,
            filtrosJson = dto.filtrosJson,
            codigoAlmacen = dto.codigoAlmacen,
            codigoArticulo = dto.codigoArticulo,
            codigoUbicacion = dto.codigoUbicacion,
            codigoOperario = dto.codigoOperario,
            estado = dto.estado,
            fechaCreacion = dto.fechaCreacion,
            fechaAsignacion = dto.fechaAsignacion,
            fechaInicio = dto.fechaInicio,
            fechaCierre = dto.fechaCierre,
            creadoPorCodigo = dto.creadoPorCodigo,
            prioridad = dto.prioridad
        )
    }
    
    fun fromLecturaConteoDto(dto: LecturaConteoDto): LecturaConteo {
        return LecturaConteo(
            guidID = dto.guidID,
            ordenGuid = dto.ordenGuid,
            codigoAlmacen = dto.codigoAlmacen,
            codigoUbicacion = dto.codigoUbicacion,
            codigoArticulo = dto.codigoArticulo,
            descripcionArticulo = dto.descripcionArticulo,
            lotePartida = dto.lotePartida,
            cantidadContada = dto.cantidadContada,
            cantidadStock = dto.cantidadStock,
            usuarioCodigo = dto.usuarioCodigo,
            fecha = dto.fecha,
            comentario = dto.comentario,
            fechaCaducidad = dto.fechaCaducidad
        )
    }
    
    fun fromResultadoConteoDto(dto: ResultadoConteoDto): ResultadoConteo {
        return ResultadoConteo(
            guidID = dto.guidID,
            ordenGuid = dto.ordenGuid,
            diferencia = dto.diferencia,
            accionFinal = dto.accionFinal,
            aprobadoPorCodigo = dto.aprobadoPorCodigo,
            fechaEvaluacion = dto.fechaEvaluacion,
            ajusteAplicado = dto.ajusteAplicado,
            codigoAlmacen = dto.codigoAlmacen,
            codigoUbicacion = dto.codigoUbicacion,
            codigoArticulo = dto.codigoArticulo,
            descripcionArticulo = dto.descripcionArticulo,
            lotePartida = dto.lotePartida,
            cantidadContada = dto.cantidadContada,
            cantidadStock = dto.cantidadStock,
            usuarioCodigo = dto.usuarioCodigo
        )
    }
    
    fun fromCerrarOrdenResponseDto(dto: CerrarOrdenResponseDto): CerrarOrdenResponse {
        return CerrarOrdenResponse(
            ordenGuid = dto.ordenGuid,
            totalLecturas = dto.totalLecturas,
            resultadosCreados = dto.resultadosCreados,
            fechaCierre = dto.fechaCierre
        )
    }
    
    fun fromLecturaPendienteDto(dto: LecturaPendienteDto): LecturaPendiente {
        return LecturaPendiente(
            codigoAlmacen = dto.codigoAlmacen,
            codigoUbicacion = dto.codigoUbicacion,
            codigoArticulo = dto.codigoArticulo,
            descripcionArticulo = dto.descripcionArticulo,
            lotePartida = dto.lotePartida,
            cantidadStock = dto.cantidadStock,
            cantidadTeorica = dto.cantidadTeorica,
            cantidadContada = null,
            fechaCaducidad = dto.fechaCaducidad
        )
    }
}
