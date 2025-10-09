package com.example.sga.view.home

import android.content.Context
import androidx.navigation.NavHostController
import com.example.sga.data.model.user.User
import com.example.sga.service.Traspasos.EstadoTraspasosService
import com.example.sga.view.app.SessionLogic
import com.example.sga.view.app.SessionViewModel
import com.example.sga.service.Inactivity.InactivityTracker
import com.example.sga.service.Ordenes.OrdenesTraspasoService
import com.example.sga.service.Conteos.ConteosService

class HomeLogic(private val sessionViewModel: SessionViewModel) {

    fun hacerLogout(user: User, context: Context, navController: NavHostController) {
        val sessionLogic = SessionLogic(sessionViewModel)

        // 🔒 Evita que salte “Su sesión ha caducado” en logout manual
        sessionViewModel.marcarTokenExpirado(false)
        sessionViewModel.ocultarMensajeCaducidad()
        InactivityTracker.stop()

        sessionLogic.cerrarSesion(context) {
            // Detener todos los servicios
            EstadoTraspasosService.detener()
            OrdenesTraspasoService.detener()
            ConteosService.detener()
            
            navController.navigate("login") {
                popUpTo(0) { inclusive = true }
            }
        }
    }

}
