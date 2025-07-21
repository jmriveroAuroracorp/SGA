package com.example.sga

import android.os.Bundle
import android.view.MotionEvent
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import com.example.sga.service.Inactivity.InactivityTracker
import com.example.sga.view.app.SGAApp
import com.example.sga.view.theme.SGATheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContent {
            SGATheme {
                SGAApp()
            }
        }
    }
    override fun dispatchTouchEvent(ev: MotionEvent): Boolean {
        InactivityTracker.resetTimer()
        return super.dispatchTouchEvent(ev)
    }
}
