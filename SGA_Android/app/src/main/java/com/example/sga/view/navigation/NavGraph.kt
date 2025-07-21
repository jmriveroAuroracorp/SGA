package com.example.sga.view.navigation

import androidx.compose.runtime.Composable
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.configuracion.ConfiguracionScreen
import com.example.sga.view.home.HomeScreen
import com.example.sga.view.login.LoginScreen
import com.example.sga.view.pesaje.PesajeScreen
import com.example.sga.view.stock.StockScreen
import com.example.sga.view.etiquetas.EtiquetasScreen
import com.example.sga.view.traspasos.TraspasosScreen
import androidx.navigation.navArgument
import androidx.navigation.NavType

@Composable
fun NavGraph(
    navController: NavHostController,
    sessionViewModel: SessionViewModel = viewModel()
) {
    NavHost(
        navController = navController,
        startDestination = "login"
    ) {
        composable("login") {
            LoginScreen(navController = navController, sessionViewModel = sessionViewModel)
        }
        composable("home") {
            HomeScreen(
                sessionViewModel = sessionViewModel,
                navController = navController
            )
        }

        composable("pesaje") {
            PesajeScreen(
                navController = navController,
                sessionViewModel = sessionViewModel
            )
        }
        composable("etiquetas") {
            EtiquetasScreen(navController,
                sessionViewModel = sessionViewModel)
        }
        composable(
            route = "traspasos/{esPalet}",
            arguments = listOf(navArgument("esPalet") { type = NavType.BoolType })
        ) { backStackEntry ->
            val esPalet = backStackEntry.arguments?.getBoolean("esPalet") ?: false

            TraspasosScreen(
                navController = navController,
                sessionViewModel = sessionViewModel,
                esPalet = esPalet
            )
        }


        composable("configuracion") {
            ConfiguracionScreen(navController, sessionViewModel)
        }

        composable("stock") {
            StockScreen(navController, sessionViewModel)
        }
    }
}