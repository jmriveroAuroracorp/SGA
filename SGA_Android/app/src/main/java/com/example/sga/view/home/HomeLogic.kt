package com.example.sga.view.home

import android.content.Context
import androidx.navigation.NavHostController
import com.example.sga.data.model.user.User
import com.example.sga.service.Traspasos.EstadoTraspasosService
import com.example.sga.view.app.SessionLogic
import com.example.sga.view.app.SessionViewModel

class HomeLogic(private val sessionViewModel: SessionViewModel) {

    fun hacerLogout(user: User, context: Context, navController: NavHostController) {
        val sessionLogic = SessionLogic(sessionViewModel)

        sessionLogic.cerrarSesion(context) {
            EstadoTraspasosService.detener()
            navController.navigate("login") {
                popUpTo(0) { inclusive = true } // limpia todo el backstack
            }
        }
    }
}
