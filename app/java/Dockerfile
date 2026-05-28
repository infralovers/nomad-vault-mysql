FROM maven:3.9-eclipse-temurin-21 AS build
WORKDIR /build

COPY pom.xml .
RUN mvn -q dependency:go-offline

COPY src ./src
RUN mvn -q package -DskipTests

FROM eclipse-temurin:21-jre AS runtime
WORKDIR /app

ENV SERVER_PORT=8080
EXPOSE 8080

COPY --from=build /build/target/nomad-vault-mysql-java-*.jar app.jar

VOLUME ["/app/config"]

ENTRYPOINT ["java", "-jar", "app.jar"]
