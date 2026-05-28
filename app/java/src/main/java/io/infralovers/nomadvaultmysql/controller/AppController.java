package io.infralovers.nomadvaultmysql.controller;

import io.infralovers.nomadvaultmysql.model.Customer;
import io.infralovers.nomadvaultmysql.model.CustomerInput;
import io.infralovers.nomadvaultmysql.service.CustomerRepository;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.ResponseEntity;
import org.springframework.stereotype.Controller;
import org.springframework.ui.Model;
import org.springframework.web.bind.annotation.*;

import java.util.List;

@Controller
public class AppController {

    private static final Logger log = LoggerFactory.getLogger(AppController.class);

    private final CustomerRepository repository;

    public AppController(CustomerRepository repository) {
        this.repository = repository;
    }

    // ── Health / API ──────────────────────────────────────────────────────────

    @GetMapping("/health")
    @ResponseBody
    public ResponseEntity<String> health() {
        if (!repository.isInitialized()) {
            return ResponseEntity.status(500).body("Unhealthy - no database");
        }
        return ResponseEntity.ok("Healthy");
    }

    @GetMapping("/customers")
    @ResponseBody
    public List<Customer> getCustomers() throws Exception {
        return repository.getCustomerRecords(50, false);
    }

    @GetMapping("/customer")
    @ResponseBody
    public ResponseEntity<?> getCustomer(@RequestParam(name = "cust_no", required = false) Integer custNo) throws Exception {
        if (custNo == null) {
            return ResponseEntity.status(500)
                    .body("<html><body>Error: cust_no is a required argument for the customer endpoint.</body></html>");
        }
        return ResponseEntity.ok(repository.getCustomerRecord(custNo));
    }

    @PostMapping("/customers")
    @ResponseBody
    public List<Customer> createCustomer(CustomerInput input) throws Exception {
        return repository.insertCustomerRecord(input);
    }

    @PutMapping("/customers")
    @ResponseBody
    public List<Customer> updateCustomer(CustomerInput input) throws Exception {
        return repository.updateCustomerRecord(input);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    @GetMapping("/")
    public String index() {
        return "index";
    }

    @GetMapping("/records")
    public String records(Model model) throws Exception {
        model.addAttribute("results", repository.getCustomerRecords(50, false));
        return "records";
    }

    @GetMapping("/dbview")
    public String dbview(Model model) throws Exception {
        model.addAttribute("results", repository.getCustomerRecords(50, true));
        return "dbview";
    }

    @GetMapping("/add")
    public String addForm() {
        return "add";
    }

    @PostMapping("/add")
    public String addSubmit(CustomerInput input, Model model) throws Exception {
        List<Customer> records = repository.insertCustomerRecord(input);
        model.addAttribute("results", records);
        model.addAttribute("recordAdded", true);
        return "records";
    }

    @GetMapping("/update")
    public String updateForm(@RequestParam(name = "cust_no", required = false) Integer custNo, Model model) throws Exception {
        if (custNo != null) {
            List<Customer> found = repository.getCustomerRecord(custNo);
            if (!found.isEmpty()) {
                model.addAttribute("customer", found.get(0));
            }
        }
        return "update";
    }

    @PostMapping("/update")
    public String updateSubmit(CustomerInput input, Model model) throws Exception {
        List<Customer> records = repository.updateCustomerRecord(input);
        model.addAttribute("results", records);
        model.addAttribute("recordUpdated", true);
        return "records";
    }
}
