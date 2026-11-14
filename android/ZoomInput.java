package clashfarmer.input;

import java.lang.reflect.Array;
import java.lang.reflect.Method;

/** A two-finger pinch through Android's input service, using the ADB shell identity. */
public final class ZoomInput {
    private final Class<?> motion = Class.forName("android.view.MotionEvent");
    private final Class<?> properties = Class.forName("android.view.MotionEvent$PointerProperties");
    private final Class<?> coordinates = Class.forName("android.view.MotionEvent$PointerCoords");
    private final Class<?> clock = Class.forName("android.os.SystemClock");
    private final Object pointers = Array.newInstance(properties, 2), points = Array.newInstance(coordinates, 2);
    private final Object input;
    private final Method inject, obtain;
    private final long downTime;

    private ZoomInput() throws Exception {
        Class<?> manager;
        try { manager = Class.forName("android.hardware.input.InputManagerGlobal"); }
        catch (ClassNotFoundException olderAndroid) { manager = Class.forName("android.hardware.input.InputManager"); }
        input = manager.getMethod("getInstance").invoke(null);
        inject = manager.getMethod("injectInputEvent", Class.forName("android.view.InputEvent"), int.class);
        obtain = motion.getMethod("obtain", long.class, long.class, int.class, int.class,
            pointers.getClass(), points.getClass(), int.class, int.class, float.class, float.class,
            int.class, int.class, int.class, int.class);
        for (int i = 0; i < 2; i++) {
            Object pointer = properties.getConstructor().newInstance();
            properties.getField("id").setInt(pointer, i);
            properties.getField("toolType").setInt(pointer, 1); // TOOL_TYPE_FINGER
            Array.set(pointers, i, pointer);
            Object point = coordinates.getConstructor().newInstance();
            coordinates.getField("pressure").setFloat(point, 1f);
            coordinates.getField("size").setFloat(point, .05f);
            Array.set(points, i, point);
        }
        downTime = uptime();
    }

    private long uptime() throws Exception { return (Long) clock.getMethod("uptimeMillis").invoke(null); }

    private void send(int action, int count, float x, float y, float radius) throws Exception {
        for (int i = 0; i < 2; i++) {
            Object point = Array.get(points, i);
            coordinates.getField("x").setFloat(point, x + (i == 0 ? -radius : radius));
            coordinates.getField("y").setFloat(point, y);
        }
        Object event = obtain.invoke(null, downTime, uptime(), action, count, pointers, points,
            0, 0, 1f, 1f, 0, 0, 4098, 0); // SOURCE_TOUCHSCREEN
        try {
            if (!(Boolean) inject.invoke(input, event, 2)) // Wait for completion.
                throw new IllegalStateException("Android rejected the pinch event");
        } finally { motion.getMethod("recycle").invoke(event); }
    }

    public static void main(String[] args) throws Exception {
        // "in" is only for opt-in home verification; the runner always zooms out.
        if (args.length < 2 || args.length > 3 || (args.length == 3 && !args[2].equals("in")))
            throw new IllegalArgumentException("Expected width, height, and optional in");
        int width = Integer.parseInt(args[0]), height = Integer.parseInt(args[1]);
        if (width < 600 || height < 300 || width > 16384 || height > 16384)
            throw new IllegalArgumentException("Invalid display size");
        boolean zoomIn = args.length == 3;
        float start = height * (zoomIn ? .025f : .28f), end = height * (zoomIn ? .28f : .025f);
        float x = width / 2f, y = height / 2f, radius = start;
        ZoomInput zoom = new ZoomInput();
        boolean touching = false;
        try {
            touching = true;
            zoom.send(0, 1, x, y, radius); // ACTION_DOWN
            zoom.send(261, 2, x, y, radius); // ACTION_POINTER_DOWN, pointer index 1
            for (int step = 1; step <= 16; step++) {
                Thread.sleep(20);
                radius = start + (end - start) * step / 16f;
                zoom.send(2, 2, x, y, radius);
            }
            zoom.send(262, 2, x, y, radius); // ACTION_POINTER_UP, pointer index 1
            zoom.send(1, 1, x, y, radius); // ACTION_UP
            touching = false;
            System.out.println(zoomIn ? "ZOOM_IN_SENT" : "ZOOM_OUT_SENT");
        } finally {
            if (touching) zoom.send(3, 2, x, y, radius); // Release contacts on failure.
        }
    }
}
