package io.infralovers.nomadvaultmysql;

import io.infralovers.nomadvaultmysql.service.CustomerRepository;
import org.springframework.boot.ApplicationRunner;
import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.boot.autoconfigure.jdbc.DataSourceAutoConfiguration;
import org.springframework.context.annotation.Bean;

@SpringBootApplication(exclude = {DataSourceAutoConfiguration.class})
public class NomadVaultMysqlApplication {

    public static void main(String[] args) {
        SpringApplication.run(NomadVaultMysqlApplication.class, args);
    }

    @Bean
    public ApplicationRunner initializer(CustomerRepository repository) {
        return args -> repository.initialize();
    }
}
