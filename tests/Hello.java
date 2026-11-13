public class Hello {
    public static void main(String[] args) throws Exception {
        System.out.println("hello-jarstart");
        System.out.println("args=" + java.util.Arrays.toString(args));
        System.err.println("some warning line");
        // 模拟 Spring Boot / Tomcat 的启动输出，用来验证端口识别
        System.out.println("Tomcat initialized with port(s): 8080 (http)");
        System.out.println("Tomcat started on port(s): 8080 (http) with context path ''");
        for (int i = 0; i < 50; i++) {
            System.out.println("tick " + i);
            Thread.sleep(200);
        }
        System.out.println("bye");
    }
}
