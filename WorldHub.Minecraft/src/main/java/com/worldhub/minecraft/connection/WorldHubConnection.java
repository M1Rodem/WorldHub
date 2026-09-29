package com.worldhub.minecraft.connection;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;

public final class WorldHubConnection {

    private static final String HOST = "127.0.0.1";
    private static final int PORT = 27071;

    private WorldHubConnection() {
    }

    public static boolean testConnection() {
        try (Socket socket = new Socket()) {
            socket.connect(
                    new InetSocketAddress(HOST, PORT),
                    1000
            );

            OutputStream output = socket.getOutputStream();
            InputStream input = socket.getInputStream();

            sendMessage(output, "WORLDHUB_HELLO");

            String response = readMessage(input);

            boolean accepted =
                    "WORLDHUB_READY".equals(response);

            System.out.println(
                    "[WorldHub] App connection: "
                            + (accepted ? "OK" : "REJECTED")
            );

            return accepted;

        } catch (IOException exception) {
            System.out.println(
                    "[WorldHub] App connection failed: "
                            + exception.getMessage()
            );

            return false;
        }
    }

    private static void sendMessage(
            OutputStream output,
            String message) throws IOException {

        byte[] data =
                message.getBytes(StandardCharsets.UTF_8);

        output.write(
                (data.length >>> 24) & 0xFF
        );
        output.write(
                (data.length >>> 16) & 0xFF
        );
        output.write(
                (data.length >>> 8) & 0xFF
        );
        output.write(
                data.length & 0xFF
        );

        output.write(data);
        output.flush();
    }

    private static String readMessage(
            InputStream input) throws IOException {

        int b1 = input.read();
        int b2 = input.read();
        int b3 = input.read();
        int b4 = input.read();

        if ((b1 | b2 | b3 | b4) < 0) {
            throw new IOException(
                    "Connection closed before response."
            );
        }

        int length =
                (b1 << 24)
                        | (b2 << 16)
                        | (b3 << 8)
                        | b4;

        if (length <= 0 || length > 1024 * 1024) {
            throw new IOException(
                    "Invalid message length: " + length
            );
        }

        byte[] data = input.readNBytes(length);

        if (data.length != length) {
            throw new IOException(
                    "Incomplete message."
            );
        }

        return new String(
                data,
                StandardCharsets.UTF_8
        );
    }
}