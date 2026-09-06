using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FutureTech.StudentManagement.Models.Entities;
using FutureTech.StudentManagement.Models.ViewModels;
using FutureTech.StudentManagement.Services;

namespace FutureTech.StudentManagement.Controllers;

[Authorize(Policy = "AdminOnly")]
public class StudentsController : Controller
{
    private readonly CosmosDbService _cosmosService;
    private readonly BlobStorageService _blobService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        CosmosDbService cosmosService,
        BlobStorageService blobService,
        ILogger<StudentsController> logger)
    {
        _cosmosService = cosmosService;
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? searchTerm, int pageNumber = 1)
    {
        const int pageSize = 10;
        var (students, totalCount) = await _cosmosService.GetStudentsAsync(searchTerm, pageNumber, pageSize);

        var viewModel = new StudentListViewModel
        {
            Students = students.Select(s => new StudentListItemViewModel
            {
                Id = s.Id,
                FullName = s.FullName,
                Email = s.Email,
                MobileNumber = s.MobileNumber,
                EnrolmentStatus = s.EnrolmentStatus.ToString(),
                ProfileImageSasUrl = !string.IsNullOrWhiteSpace(s.ProfileImageBlobName)
                    ? _blobService.GenerateSasToken(s.ProfileImageBlobName, 60)
                    : null,
                CreatedAt = s.CreatedAt
            }).ToList(),
            SearchTerm = searchTerm,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return View(viewModel);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateStudentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existing = await _cosmosService.GetStudentByEmailAsync(model.Email);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(model.Email), "A student with this email already exists.");
            return View(model);
        }

        try
        {
            var student = new Student
            {
                FirstName = model.FirstName.Trim(),
                LastName = model.LastName.Trim(),
                Email = model.Email.Trim(),
                MobileNumber = model.MobileNumber.Trim(),
                EnrolmentStatus = model.EnrolmentStatus
            };

            if (model.ProfileImage != null)
            {
                student.ProfileImageBlobName = await _blobService.UploadProfileImageAsync(model.ProfileImage, student.Id);
                student.ProfileImageUrl = _blobService.GetBlobUrl(student.ProfileImageBlobName);
            }

            await _cosmosService.CreateStudentAsync(student);
            TempData["SuccessMessage"] = "Student created successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create student {Email}", model.Email);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    public async Task<IActionResult> Edit(string id)
    {
        var student = await _cosmosService.GetStudentAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        var viewModel = new EditStudentViewModel
        {
            Id = student.Id,
            FirstName = student.FirstName,
            LastName = student.LastName,
            Email = student.Email,
            MobileNumber = student.MobileNumber,
            EnrolmentStatus = student.EnrolmentStatus,
            ExistingImageSasUrl = !string.IsNullOrWhiteSpace(student.ProfileImageBlobName)
                ? _blobService.GenerateSasToken(student.ProfileImageBlobName, 60)
                : null
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, EditStudentViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var student = await _cosmosService.GetStudentAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        var existing = await _cosmosService.GetStudentByEmailAsync(model.Email);
        if (existing != null && existing.Id != id)
        {
            ModelState.AddModelError(nameof(model.Email), "A student with this email already exists.");
            return View(model);
        }

        try
        {
            if (model.NewProfileImage != null)
            {
                await _blobService.DeleteImageAsync(student.ProfileImageBlobName);
                student.ProfileImageBlobName = await _blobService.UploadProfileImageAsync(model.NewProfileImage, student.Id);
                student.ProfileImageUrl = _blobService.GetBlobUrl(student.ProfileImageBlobName);
            }

            student.FirstName = model.FirstName.Trim();
            student.LastName = model.LastName.Trim();
            student.Email = model.Email.Trim();
            student.MobileNumber = model.MobileNumber.Trim();
            student.EnrolmentStatus = model.EnrolmentStatus;

            await _cosmosService.UpdateStudentAsync(student);
            TempData["SuccessMessage"] = "Student updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update student {StudentId}", id);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SoftDelete(string id)
    {
        await _cosmosService.SoftDeleteStudentAsync(id);
        TempData["SuccessMessage"] = "Student marked as inactive.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDelete(string id)
    {
        var student = await _cosmosService.GetStudentAsync(id);
        if (student == null)
        {
            return NotFound();
        }

        await _blobService.DeleteImageAsync(student.ProfileImageBlobName);
        await _cosmosService.HardDeleteStudentAsync(id);
        TempData["SuccessMessage"] = "Student permanently deleted.";
        return RedirectToAction(nameof(Index));
    }
}
