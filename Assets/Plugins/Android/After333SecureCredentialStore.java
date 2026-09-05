package com.after333.security;

import android.content.Context;
import android.content.SharedPreferences;
import android.security.keystore.KeyGenParameterSpec;
import android.security.keystore.KeyProperties;
import android.util.Base64;
import android.util.Log;

import com.unity3d.player.UnityPlayer;

import java.nio.charset.StandardCharsets;
import java.security.KeyStore;
import java.util.Arrays;

import javax.crypto.Cipher;
import javax.crypto.KeyGenerator;
import javax.crypto.SecretKey;
import javax.crypto.spec.GCMParameterSpec;

public final class After333SecureCredentialStore {
    private static final String TAG = "After333SecureStore";
    private static final String ANDROID_KEY_STORE = "AndroidKeyStore";
    private static final String KEY_ALIAS = "After333.AccountCredentials.v1";
    private static final String PREFERENCES_NAME = "after333_secure_credentials";
    private static final String CIPHER_TRANSFORMATION = "AES/GCM/NoPadding";
    private static final int FORMAT_VERSION = 1;
    private static final int IV_LENGTH_BYTES = 12;
    private static final int GCM_TAG_LENGTH_BITS = 128;

    private After333SecureCredentialStore() {
    }

    public static String getString(String key) {
        if (isBlank(key)) {
            return null;
        }

        String encoded = preferences().getString(key, null);
        if (encoded == null || encoded.isEmpty()) {
            return null;
        }

        try {
            byte[] payload = Base64.decode(encoded, Base64.NO_WRAP);
            if (payload.length <= 1 + IV_LENGTH_BYTES || payload[0] != FORMAT_VERSION) {
                Log.e(TAG, "Unsupported encrypted credential format.");
                return null;
            }

            byte[] iv = Arrays.copyOfRange(payload, 1, 1 + IV_LENGTH_BYTES);
            byte[] encrypted = Arrays.copyOfRange(payload, 1 + IV_LENGTH_BYTES, payload.length);
            Cipher cipher = Cipher.getInstance(CIPHER_TRANSFORMATION);
            cipher.init(
                Cipher.DECRYPT_MODE,
                getOrCreateSecretKey(),
                new GCMParameterSpec(GCM_TAG_LENGTH_BITS, iv));
            cipher.updateAAD(key.getBytes(StandardCharsets.UTF_8));
            byte[] plainText = cipher.doFinal(encrypted);
            return new String(plainText, StandardCharsets.UTF_8);
        } catch (Exception exception) {
            Log.e(TAG, "Failed to decrypt an account credential.", exception);
            return null;
        }
    }

    public static boolean setString(String key, String value) {
        if (isBlank(key) || value == null) {
            return false;
        }

        try {
            Cipher cipher = Cipher.getInstance(CIPHER_TRANSFORMATION);
            // Android Keystore rejects caller-provided IVs when randomized
            // encryption is required. Let the provider generate a safe IV.
            cipher.init(Cipher.ENCRYPT_MODE, getOrCreateSecretKey());
            byte[] iv = cipher.getIV();
            if (iv == null || iv.length != IV_LENGTH_BYTES) {
                Log.e(TAG, "Android Keystore returned an invalid GCM IV.");
                return false;
            }
            cipher.updateAAD(key.getBytes(StandardCharsets.UTF_8));
            byte[] encrypted = cipher.doFinal(value.getBytes(StandardCharsets.UTF_8));

            byte[] payload = new byte[1 + iv.length + encrypted.length];
            payload[0] = FORMAT_VERSION;
            System.arraycopy(iv, 0, payload, 1, iv.length);
            System.arraycopy(encrypted, 0, payload, 1 + iv.length, encrypted.length);
            String encoded = Base64.encodeToString(payload, Base64.NO_WRAP);
            return preferences().edit().putString(key, encoded).commit();
        } catch (Exception exception) {
            Log.e(TAG, "Failed to encrypt an account credential.", exception);
            return false;
        }
    }

    public static boolean deleteKey(String key) {
        if (isBlank(key)) {
            return false;
        }

        return preferences().edit().remove(key).commit();
    }

    private static synchronized SecretKey getOrCreateSecretKey() throws Exception {
        KeyStore keyStore = KeyStore.getInstance(ANDROID_KEY_STORE);
        keyStore.load(null);
        if (keyStore.containsAlias(KEY_ALIAS)) {
            KeyStore.SecretKeyEntry entry =
                (KeyStore.SecretKeyEntry) keyStore.getEntry(KEY_ALIAS, null);
            return entry.getSecretKey();
        }

        KeyGenerator generator = KeyGenerator.getInstance(
            KeyProperties.KEY_ALGORITHM_AES,
            ANDROID_KEY_STORE);
        generator.init(new KeyGenParameterSpec.Builder(
            KEY_ALIAS,
            KeyProperties.PURPOSE_ENCRYPT | KeyProperties.PURPOSE_DECRYPT)
            .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
            .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
            .setRandomizedEncryptionRequired(true)
            .setKeySize(256)
            .build());
        return generator.generateKey();
    }

    private static SharedPreferences preferences() {
        Context context = UnityPlayer.currentActivity.getApplicationContext();
        return context.getSharedPreferences(PREFERENCES_NAME, Context.MODE_PRIVATE);
    }

    private static boolean isBlank(String value) {
        return value == null || value.trim().isEmpty();
    }
}
