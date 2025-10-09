package com.example.sga.utils

import android.content.Context
import android.media.AudioManager
import android.media.ToneGenerator
import android.media.SoundPool
import android.os.Vibrator
import android.os.VibrationEffect
import android.util.Log
import android.media.RingtoneManager
import android.media.Ringtone
import android.os.Build

class SoundUtils private constructor() {
    private var toneGenerator: ToneGenerator? = null
    private var vibrator: Vibrator? = null
    private var ringtone: Ringtone? = null
    private var soundPool: SoundPool? = null
    private var successSoundId: Int = 0
    private var errorSoundId: Int = 0
    private var isInitialized = false
    private var isHoneywellDevice = false

    companion object {
        @Volatile
        private var INSTANCE: SoundUtils? = null

        fun getInstance(): SoundUtils {
            return INSTANCE ?: synchronized(this) {
                INSTANCE ?: SoundUtils().also { INSTANCE = it }
            }
        }
    }

    fun initialize(context: Context) {
        if (isInitialized) return

        try {
            val audioManager = context.getSystemService(Context.AUDIO_SERVICE) as AudioManager
            // Detectar dispositivo Honeywell (fabricante/marca)
            val manufacturer = Build.MANUFACTURER ?: ""
            val brand = Build.BRAND ?: ""
            isHoneywellDevice = manufacturer.equals("honeywell", ignoreCase = true) ||
                brand.equals("honeywell", ignoreCase = true)
            
            // VOLUMEN MÁS SUAVE PARA EVITAR DISTORSIÓN
            val maxVolumeAlarm = audioManager.getStreamMaxVolume(AudioManager.STREAM_ALARM)
            val maxVolumeMusic = audioManager.getStreamMaxVolume(AudioManager.STREAM_MUSIC)
            val maxVolumeNotification = audioManager.getStreamMaxVolume(AudioManager.STREAM_NOTIFICATION)
            
            // USAR 60% DEL VOLUMEN MÁXIMO PARA EVITAR DISTORSIÓN
            val volumeAlarm = (maxVolumeAlarm * 0.6).toInt()
            val volumeMusic = (maxVolumeMusic * 0.6).toInt()
            val volumeNotification = (maxVolumeNotification * 0.6).toInt()
            
            audioManager.setStreamVolume(AudioManager.STREAM_ALARM, volumeAlarm, 0)
            audioManager.setStreamVolume(AudioManager.STREAM_MUSIC, volumeMusic, 0)
            audioManager.setStreamVolume(AudioManager.STREAM_NOTIFICATION, volumeNotification, 0)
            
            // USAR VOLUMEN MÁS SUAVE
            toneGenerator = ToneGenerator(AudioManager.STREAM_ALARM, volumeAlarm)
            vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE)?.let { vibratorManager ->
                    (vibratorManager as android.os.VibratorManager).defaultVibrator
                }
            } else {
                @Suppress("DEPRECATION")
                context.getSystemService(Context.VIBRATOR_SERVICE) as Vibrator
            }
            
            // RINGTONE COMO FALLBACK PARA HONEYWELL
            val notificationUri = RingtoneManager.getDefaultUri(RingtoneManager.TYPE_NOTIFICATION)
            ringtone = RingtoneManager.getRingtone(context, notificationUri)
            
            // SOUNDPOOL PARA SONIDOS PERSONALIZADOS (adaptativo por dispositivo)
            soundPool = SoundPool.Builder()
                .setMaxStreams(1)
                .setAudioAttributes(
                    android.media.AudioAttributes.Builder()
                        .setUsage(
                            if (isHoneywellDevice) android.media.AudioAttributes.USAGE_ALARM
                            else android.media.AudioAttributes.USAGE_NOTIFICATION
                        )
                        .setContentType(android.media.AudioAttributes.CONTENT_TYPE_SONIFICATION)
                        .build()
                )
                .build()
            
            // CARGAR SONIDOS PERSONALIZADOS SI EXISTEN
            loadCustomSounds(context)
            
            isInitialized = true
            
            Log.d("SoundUtils", "✅ VOLUMEN MÁXIMO EN TODOS LOS STREAMS - ALARM:$maxVolumeAlarm MUSIC:$maxVolumeMusic NOTIF:$maxVolumeNotification")
        } catch (e: Exception) {
            Log.e("SoundUtils", "❌ Error inicializando sonidos: ${e.message}")
        }
    }

    fun playSuccessSound() {
        Log.d("SoundUtils", "🔊 ÉXITO - FUNCIÓN LLAMADA - ${System.currentTimeMillis()}")
        if (!isInitialized) {
            Log.w("SoundUtils", "⚠️ Sonidos no inicializados")
            return
        }

        try {
            Log.d("SoundUtils", "🔊 ÉXITO - INTENTANDO SONIDO PERSONALIZADO")
            
            // INTENTAR SONIDO PERSONALIZADO PRIMERO - ADAPTATIVO POR DISPOSITIVO
            if (successSoundId > 0) {
                val (vol, rate) = if (isHoneywellDevice) 0.6f to 0.9f else 0.9f to 1.0f
                soundPool?.play(successSoundId, vol, vol, 1, 0, rate)
                Log.d("SoundUtils", "🔊 ÉXITO - SONIDO PERSONALIZADO REPRODUCIDO")
            } else {
                // FALLBACK A TONO DEL SISTEMA - UN SOLO TONO
                toneGenerator?.startTone(ToneGenerator.TONE_CDMA_ANSWER, 400)
                Log.d("SoundUtils", "🔊 ÉXITO - TONO DEL SISTEMA")
            }
            
            // Vibración de éxito: UNA CORTA (SÍ)
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
                vibrator?.vibrate(VibrationEffect.createOneShot(200, VibrationEffect.DEFAULT_AMPLITUDE))
            } else {
                @Suppress("DEPRECATION")
                vibrator?.vibrate(200)
            }
            
        } catch (e: Exception) {
            Log.e("SoundUtils", "❌ Error reproduciendo sonido de éxito: ${e.message}")
        }
    }

    fun playErrorSound() {
        Log.d("SoundUtils", "🔊 ERROR - FUNCIÓN LLAMADA")
        if (!isInitialized) {
            Log.w("SoundUtils", "⚠️ Sonidos no inicializados")
            return
        }

        try {
            Log.d("SoundUtils", "🔊 ERROR - INTENTANDO SONIDO PERSONALIZADO")
            
            // INTENTAR SONIDO PERSONALIZADO PRIMERO - ADAPTATIVO POR DISPOSITIVO
            if (errorSoundId > 0) {
                val vol = if (isHoneywellDevice) 0.8f else 0.9f
                soundPool?.play(errorSoundId, vol, vol, 1, 0, 1.0f)
                Log.d("SoundUtils", "🔊 ERROR - SONIDO PERSONALIZADO REPRODUCIDO")
            } else {
                // FALLBACK A TONO DEL SISTEMA - UN SOLO TONO
                toneGenerator?.startTone(ToneGenerator.TONE_CDMA_ALERT_CALL_GUARD, 600)
                Log.d("SoundUtils", "🔊 ERROR - TONO DEL SISTEMA")
            }
            
            // Vibración de error: DOS CORTAS (NO)
            if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
                vibrator?.vibrate(VibrationEffect.createWaveform(longArrayOf(0, 150, 100, 150), -1))
            } else {
                @Suppress("DEPRECATION")
                vibrator?.vibrate(longArrayOf(0, 150, 100, 150), -1)
            }
            
        } catch (e: Exception) {
            Log.e("SoundUtils", "❌ Error reproduciendo sonido de error: ${e.message}")
        }
    }

    private fun loadCustomSounds(context: Context) {
        try {
            // CARGAR SONIDOS DESDE RESOURCES (SI EXISTEN)
            try {
                soundPool?.load(context, com.example.sga.R.raw.success, 1)?.let { soundId ->
                    successSoundId = soundId
                    Log.d("SoundUtils", "✅ Sonido de éxito cargado desde resources")
                } ?: run {
                    successSoundId = 0
                    Log.d("SoundUtils", "⚠️ No se pudo cargar success.wav")
                }
            } catch (e: Exception) {
                Log.d("SoundUtils", "⚠️ No se encontró success.wav en resources")
                successSoundId = 0
            }
            
            try {
                soundPool?.load(context, com.example.sga.R.raw.error, 1)?.let { soundId ->
                    errorSoundId = soundId
                    Log.d("SoundUtils", "✅ Sonido de error cargado desde resources")
                } ?: run {
                    errorSoundId = 0
                    Log.d("SoundUtils", "⚠️ No se pudo cargar error.wav")
                }
            } catch (e: Exception) {
                Log.d("SoundUtils", "⚠️ No se encontró error.wav en resources")
                errorSoundId = 0
            }
            
        } catch (e: Exception) {
            Log.e("SoundUtils", "❌ Error cargando sonidos personalizados: ${e.message}")
        }
    }

    fun reloadCustomSounds(context: Context) {
        loadCustomSounds(context)
    }

}
