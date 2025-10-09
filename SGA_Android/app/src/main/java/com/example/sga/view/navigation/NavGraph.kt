package com.example.sga.view.navigation

import androidx.compose.runtime.Composable
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import com.example.sga.view.conteos.ConteoLogic
import com.example.sga.view.conteos.ConteoViewModel
import com.example.sga.view.app.SessionViewModel
import com.example.sga.view.configuracion.ConfiguracionScreen
import com.example.sga.view.home.HomeScreen
import com.example.sga.view.login.LoginScreen
import com.example.sga.view.pesaje.PesajeScreen
import com.example.sga.view.stock.StockScreen
import com.example.sga.view.etiquetas.EtiquetasScreen
import com.example.sga.view.traspasos.TraspasosScreen
import com.example.sga.view.conteos.ConteoScreen
import com.example.sga.view.conteos.ConteoProcesoScreen
import com.example.sga.view.ordenes.OrdenTraspasoScreen
import com.example.sga.view.ordenes.OrdenTraspasoProcesoScreen
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
            route = "traspasos/{esPalet}/{directoDesdePaletCerrado}",
            arguments = listOf(
                navArgument("esPalet") {
                    type = NavType.BoolType
                    defaultValue = false
                },
                navArgument("directoDesdePaletCerrado") {
                    type = NavType.BoolType
                    defaultValue = false
                }
            )
        ) { backStackEntry ->
            val esPalet = backStackEntry.arguments?.getBoolean("esPalet") ?: false
            val directo = backStackEntry.arguments?.getBoolean("directoDesdePaletCerrado") ?: false

            TraspasosScreen(
                navController = navController,
                sessionViewModel = sessionViewModel,
                esPalet = esPalet,
                directoDesdePaletCerrado = directo
            )
        }


        composable("configuracion") {
            ConfiguracionScreen(navController, sessionViewModel)
        }

        composable("stock") {
            StockScreen(navController, sessionViewModel)
        }
        
        composable("conteos") {
            // Usar ViewModel compartido para reutilizar datos ya cargados
            val conteoViewModel: ConteoViewModel = viewModel(key = "ConteoViewModel")
            val conteoLogic = ConteoLogic(conteoViewModel)
            
            ConteoScreen(
                conteoViewModel = conteoViewModel,
                conteoLogic = conteoLogic,
                sessionViewModel = sessionViewModel,
                navController = navController,
                onNavigateToDetail = { ordenGuid ->
                    navController.navigate("conteos/proceso/$ordenGuid")
                }
            )
        }
        
        composable(
            route = "conteos/proceso/{ordenGuid}",
            arguments = listOf(
                navArgument("ordenGuid") {
                    type = NavType.StringType
                }
            )
        ) { backStackEntry ->
            val ordenGuid = backStackEntry.arguments?.getString("ordenGuid") ?: ""
            val conteoViewModel: ConteoViewModel = viewModel(key = "ConteoViewModel")
            val conteoLogic = ConteoLogic(conteoViewModel)
            
            ConteoProcesoScreen(
                ordenGuid = ordenGuid,
                conteoViewModel = conteoViewModel,
                conteoLogic = conteoLogic,
                sessionViewModel = sessionViewModel,
                navController = navController
            )
        }
        
        composable("ordenes") {
            android.util.Log.d("NAVGRAPH", "🔍 Ruta 'ordenes' registrada")
            OrdenTraspasoScreen(
                sessionViewModel = sessionViewModel,
                navController = navController,
                onNavigateToDetail = { ordenId ->
                    android.util.Log.d("NAVGRAPH", "🔍 Navegando desde ordenes a: ordenes/proceso/$ordenId")
                    navController.navigate("ordenes/proceso/$ordenId")
                }
            )
        }
        
        composable(
            route = "ordenes/proceso/{ordenId}",
            arguments = listOf(
                navArgument("ordenId") {
                    type = NavType.StringType
                }
            )
        ) { backStackEntry ->
            val ordenId = backStackEntry.arguments?.getString("ordenId") ?: ""
            android.util.Log.d("NAVGRAPH", "🔍 Ruta 'ordenes/proceso/{ordenId}' registrada")
            android.util.Log.d("NAVGRAPH", "🔍 ordenId recibido: $ordenId")
            
            OrdenTraspasoProcesoScreen(
                ordenId = ordenId,
                sessionViewModel = sessionViewModel,
                navController = navController
            )
        }
    }
}
