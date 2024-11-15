using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Abp.Authorization;
using Abp.Authorization.Users;
using Abp.Collections.Extensions;
using Abp.Domain.Repositories;
using Abp.Linq.Extensions;
using Abp.Logging;
using Abp.UI;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pulse.Authorization;
using Pulse.Authorization.Roles;
using Pulse.Authorization.Users;
using Pulse.CustomEnum;
using Pulse.RubixProduct.Dtos;
using Pulse.RubixProduct.Dtos.CustomDtos;
using Pulse.RubixProduct.Exporting;
using Twilio.TwiML.Messaging;
using static Pulse.RubixProduct.Dtos.AttendanceDto;

namespace Pulse.RubixProduct
{
    [AbpAuthorize(AppPermissions.Pages_Attendances)]
    public class AttendancesAppService
        : AttendancesAppServiceBase,
            IAttendancesAppService,
            IAttendancesAppServiceExtended
    {
        private readonly IRepository<User, long> _lookup_userRepository;
        private readonly IRepository<Attendance> _attendanceRepository;
        private readonly IRepository<UserDetail> _userDetailRepository;
        private readonly IRepository<PermissionRequest> _permissionRequestRepository;
        private readonly IRepository<Holiday> _holidayRepository;
        private readonly IRepository<LeaveDetail> _leaveDetailRepository;
        private readonly IRepository<Project> _projectRepository;
        private readonly IRepository<Client> _clientRepository;
        private readonly IRepository<TaskGroupDetail, int> _lookup_taskGroupDetailRepository;
        private readonly IRepository<UserDetail> _userDetailsRepository;
        private readonly IRepository<ProjectUserMapping> _prjUserMapRepository;
        private readonly IRepository<UserRole, long> _userRoleRepository;
        private readonly IRepository<Role> _roleRepository;
        private readonly IRepository<Leave> _leaveRepository;
        private readonly IRepository<LeaveType> _leavetypeRepository;


        public AttendancesAppService(
            IRepository<Attendance> attendanceRepository,
            IAttendancesExcelExporter attendancesExcelExporter,
            IRepository<User, long> lookup_userRepository,
            IRepository<UserDetail> userDetailRepository,
            IRepository<PermissionRequest> permissionRequestRepository,
            IRepository<Holiday> holidayRepository,
            IRepository<LeaveDetail> leaveDetailRepository,
            IRepository<Project> projectRepository,
            IRepository<UserDetail> userDetailsRepositor,
            IRepository<Client> clientRepository,
            IRepository<ProjectUserMapping> prjUserMapRepository,
            IRepository<TaskGroupDetail, int> lookup_taskGroupDetailRepository,
            IRepository<UserRole, long> userRoleRepository,
            IRepository<Role> roleRepository,
            IRepository<Leave> leaveRepository,
            IRepository<LeaveType> leavetypeRepository
        )
            : base(attendanceRepository, attendancesExcelExporter, lookup_userRepository)
        {
            _permissionRequestRepository = permissionRequestRepository;
            _attendanceRepository = attendanceRepository;
            _lookup_userRepository = lookup_userRepository;
            _userDetailRepository = userDetailRepository;
            _holidayRepository = holidayRepository;
            _leaveDetailRepository = leaveDetailRepository;
            _projectRepository = projectRepository;
            _clientRepository = clientRepository;
            _lookup_userRepository = lookup_userRepository;
            _lookup_taskGroupDetailRepository = lookup_taskGroupDetailRepository;
            _userDetailsRepository = userDetailsRepositor;
            _prjUserMapRepository = prjUserMapRepository;
            _userRoleRepository = userRoleRepository;
            _roleRepository = roleRepository;
            _leaveRepository = leaveRepository;
            _leavetypeRepository = leavetypeRepository;
        }


        public virtual async Task<AttendanceDetailsForFooter> GetAllCustom(GetAllAttendancesInput input)
        {
            var results = new AttendanceDetailsForFooter();
            int payableHoursLimit = 540;
            results.FooterDetails = new FooterDetails
            {
                PayableHours = 0,
                PresentHours = 0,
                PaidLeave = 0,
                UnPaidLeave = 0,
                Weekend = 0,
                Holidays = 0,
                Absent = 0
            };
            results.AttendanceDetails = new List<AttendanceDetails>();

            try
            {
                var filteredAttendances = _attendanceRepository
               .GetAll()
               .Include(e => e.UserFk)
               .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), e => e.EmployeeId.Contains(input.Filter) || e.SourceType.Contains(input.Filter))
               .WhereIf(input.MinCheckInFilter != null, e => e.CheckIn >= input.MinCheckInFilter)
               .WhereIf(input.MaxCheckInFilter != null, e => e.CheckIn <= input.MaxCheckInFilter)
               .WhereIf(input.MinCheckOutFilter != null, e => e.CheckOut >= input.MinCheckOutFilter)
               .WhereIf(input.MaxCheckOutFilter != null, e => e.CheckOut <= input.MaxCheckOutFilter)
               .WhereIf(input.MinTotalMinutesFilter != null, e => e.TotalMinutes >= input.MinTotalMinutesFilter)
               .WhereIf(input.MaxTotalMinutesFilter != null, e => e.TotalMinutes <= input.MaxTotalMinutesFilter)
               .WhereIf(!string.IsNullOrWhiteSpace(input.EmployeeIdFilter), e => e.EmployeeId.Contains(input.EmployeeIdFilter))
               .WhereIf(input.MinEventtimeFilter != null, e => e.Eventtime >= input.MinEventtimeFilter)
               .WhereIf(input.MaxEventtimeFilter != null, e => e.Eventtime <= input.MaxEventtimeFilter)
               .WhereIf(input.IscheckinFilter.HasValue && input.IscheckinFilter > -1, e => (input.IscheckinFilter == 1 && e.Ischeckin) || (input.IscheckinFilter == 0 && !e.Ischeckin))
               .WhereIf(input.MinDownloaddateFilter != null, e => e.Downloaddate >= input.MinDownloaddateFilter)
               .WhereIf(input.MaxDownloaddateFilter != null, e => e.Downloaddate <= input.MaxDownloaddateFilter)
               .WhereIf(!string.IsNullOrWhiteSpace(input.SourceTypeFilter), e => e.SourceType.Contains(input.SourceTypeFilter))
               .WhereIf(!string.IsNullOrWhiteSpace(input.UserNameFilter), e => e.UserFk != null && e.UserFk.Id == long.Parse(input.UserNameFilter))
               .WhereIf(input.FromDate != null && input.ToDate != null, e => e.Eventtime.Date >= input.FromDate.Value.Date && e.Eventtime.Date <= input.ToDate.Value.Date)
               .WhereIf(input.Month != null, e => e.Eventtime.Date.Year == input.Month.Value.Year && e.Eventtime.Date.Month == input.Month.Value.Month)
               .GroupBy(e => e.Eventtime.Date)
               .Select(group => new
               {
                   Date = group.Key,
                   //Regularization record Updation
                   FirstCheckin = group.Where(e => e.RegularizationActive == true).OrderByDescending(e => e.Eventtime).FirstOrDefault(e => e.Ischeckin == true) ?? group.FirstOrDefault(e => e.Ischeckin == true),

                   LastCheckout = group.OrderByDescending(e => e.Eventtime).FirstOrDefault(e => e.Ischeckin == false),
               });

                int totalCount = await filteredAttendances.CountAsync();
                var dbList = await filteredAttendances.ToListAsync();

                int paidBreak = 60;
                DateTime fromDate = new DateTime();
                DateTime toDate = new DateTime();

                if (input.FromDate != null && input.ToDate != null)
                {
                    fromDate = (DateTime)input.FromDate;
                    toDate = (DateTime)input.ToDate;
                }
                else
                {
                    fromDate = new DateTime(input.Month.Value.Year, input.Month.Value.Month, 1);
                    toDate = fromDate.AddMonths(1).AddDays(-1);
                }

                // Get the user's join date
                var user = _userDetailRepository.GetAll().FirstOrDefault(u => u.UserId == long.Parse(input.UserNameFilter));
                var joinDate = user?.DateOfJoin;

                if (dbList.Count == 0 || dbList == null)
                {
                    for (DateTime date = fromDate; date <= toDate; date = date.AddDays(1))
                    {
                        string status = "";
                        int leavePayableHours = 0;
                        int presentPayableHours = 0;
                        int weekendPayableHours = 0;
                        int holidayPayableHours = 0;
                        int permissionPayableHours = 0;
                        int unpaidLeavePayableHours = 0;
                        Holiday holiday = _holidayRepository.GetAll().FirstOrDefault(h => h.Date.Date == date.Date);
                        LeaveDetail leaves = _leaveDetailRepository
                            .GetAll()
                            .Include(l => l.LeaveFk.LeaveTypeFk)
                            .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == date.Date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                        var permissionWFH = await _permissionRequestRepository
                            .GetAll()
                            .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                            .Where(x => x.PermissionOn.Value.Date == date.Date)
                            .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome || x.PermissionType == CustomEnum.PermissionType.Permission)
                            .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                            .ToListAsync();

                        PermissionRequest wfh = permissionWFH.Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome).FirstOrDefault();
                        PermissionRequest permission = permissionWFH.Where(x => x.PermissionType == CustomEnum.PermissionType.Permission).FirstOrDefault();

                        if (holiday != null)
                        {
                            status = holiday.HolidayName + " (Holiday)";
                            results.FooterDetails.Holidays += (payableHoursLimit);
                            holidayPayableHours = payableHoursLimit;
                        }
                        else if ((date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) && holiday == null)
                        {
                            status = "Weekend";
                            results.FooterDetails.Weekend += (payableHoursLimit);
                            weekendPayableHours = payableHoursLimit;
                        }
                        else if (leaves != null && wfh == null)
                        {
                            int halfDayMinutes = 270; // Half-day in minutes
                            bool isFutureDate = date.Date > DateTime.Now.Date;

                            // Fetch both HalfDayFirstHalf and HalfDaySecondHalf leaves for the same date
                            var halfDayLeaves = _leaveDetailRepository
                                .GetAll()
                                .Include(l => l.LeaveFk)
                                .ThenInclude(l => l.LeaveTypeFk)
                                .Where(l => l.CreatorUserId == long.Parse(input.UserNameFilter)
                                && l.Date.Value.Date == date.Date
                                && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved
                                && (l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf || l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf))
                                .ToList();

                            // Case: Both first half and second half leave exist (could be mixed LOP and Paid Leave)
                            if (halfDayLeaves.Count == 2)
                            {
                                var firstHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                var statusParts = new List<string>();

                                // Check and process the first half leave
                                if (firstHalfLeave != null)
                                {
                                    string firstHalfLeaveType = firstHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "FirstHalf Leave Type Missing";
                                    if (firstHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // First half is LOP
                                    {
                                        statusParts.Add($"LOP ( FirstHalf)");
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        /*leavePayableHours = halfDayMinutes;*/
                                        unpaidLeavePayableHours = halfDayMinutes;
                                    }
                                    else // First half is Paid Leave
                                    {
                                        statusParts.Add($"{firstHalfLeaveType} ( FirstHalf)");
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                        leavePayableHours = halfDayMinutes;
                                    }
                                }

                                // Check and process the second half leave
                                if (secondHalfLeave != null)
                                {
                                    string secondHalfLeaveType = secondHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "SecondHalf Leave Type Missing";
                                    if (secondHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // Second half is LOP
                                    {
                                        statusParts.Add($"LOP ( SecondHalf)");
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        /* leavePayableHours += halfDayMinutes;*/
                                        unpaidLeavePayableHours += halfDayMinutes;
                                    }
                                    else // Second half is Paid Leave
                                    {
                                        statusParts.Add($"{secondHalfLeaveType} (SecondHalf)");
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                        leavePayableHours += halfDayMinutes;
                                    }
                                }

                                // Combine the status parts
                                status = string.Join("  ", statusParts);
                            }

                            else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Check if the leave is unpaid
                            {
                                if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf) // LOP for first half
                                {
                                    halfDayMinutes = 270;
                                    if (halfDayMinutes == 270)
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) Leave and 0.5 Absent";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        /* leavePayableHours = halfDayMinutes;*/
                                        unpaidLeavePayableHours += halfDayMinutes;
                                    }

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    halfDayMinutes = 270;
                                    if (halfDayMinutes == 270)
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) Leave  and 0.5 Absent";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes; // Add to unpaid leave for second half
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        /* leavePayableHours = halfDayMinutes; // Track unpaid hours*/
                                        unpaidLeavePayableHours += halfDayMinutes;
                                    }

                                }
                                else // Full day LOP
                                {
                                    status = " LOP ";
                                    results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                    /*leavePayableHours = payableHoursLimit;*/
                                    unpaidLeavePayableHours = payableHoursLimit;
                                }
                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && date.Date > DateTime.Now.Date)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + " (FirstHalf) Leave";
                                leavePayableHours = halfDayMinutes;
                                results.FooterDetails.PaidLeave += halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && date.Date > DateTime.Now.Date)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) Leave";
                                leavePayableHours = halfDayMinutes;
                                results.FooterDetails.PaidLeave += halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf) Leave and 0.5 Absent";
                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                results.FooterDetails.Absent += halfDayMinutes;
                                leavePayableHours = halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) Leave 0.5 Absent";
                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                results.FooterDetails.Absent += halfDayMinutes;
                                leavePayableHours = halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                results.FooterDetails.PaidLeave += payableHoursLimit;
                                leavePayableHours = payableHoursLimit;
                            }

                        }


                        else if (wfh != null && leaves == null) // Check WFH approval status
                        {
                            var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;
                            if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                            {
                                status = "Absent";
                                results.FooterDetails.Absent += payableHoursLimit;

                            }
                            else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for WFH hours between 4 and 9 hours
                            {
                                // Calculate present hours dynamically based on WFH minutes
                                double presentHours = wfhMinutes / 60.0;
                                presentPayableHours = Convert.ToInt32(presentHours * 60);


                                if (wfhMinutes == 4 * 60)
                                {
                                    presentPayableHours = ((int)(4.5 * 60)); // 4 hours of WFH results in 4.5 hours present
                                }

                                status = "0.5 P (WFH) | 0.5 A";
                                results.FooterDetails.PresentHours += presentPayableHours;
                                results.FooterDetails.Absent += 270;

                            }
                            else
                            {
                                status = "Present (WFH)";
                                results.FooterDetails.PresentHours += payableHoursLimit;
                                presentPayableHours = payableHoursLimit;
                            }
                        }
                        else if (permission != null)
                        {
                            var permissionMinutes = ((permission.ToTime.Value.TimeOfDay - permission.FromTime.Value.TimeOfDay) * 60).TotalHours;
                            status = "";
                            permissionPayableHours += Convert.ToInt32(permissionMinutes);

                        }
                        else if (leaves != null && wfh != null) // Check for both WFH and Leave on the same day
                        {
                            int halfDayMinutes = 270;
                            var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;

                            // Case 1: Half-day Leave (First Half) and sufficient WFH time
                            if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                            {
                                if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // LOP for first half
                                {
                                    status = "LOP ( FirstHalf) and 0.5 Present (WFH)";
                                    results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                }
                                else // Paid leave for first half
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " (FirstHalf) Leave and 0.5 Present (WFH)";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                }

                                results.FooterDetails.PresentHours += halfDayMinutes;
                                /* leavePayableHours = halfDayMinutes;*/
                                unpaidLeavePayableHours += halfDayMinutes;
                                presentPayableHours = halfDayMinutes;
                            }

                            // Case 2: Half-day Leave (Second Half) and sufficient WFH time
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                            {
                                if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // LOP for second half
                                {
                                    status = "LOP ( SecondHalf) and 0.5 Present (WFH)";
                                    results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                }
                                else // Paid leave for second half
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " (SecondHalf) Leave and 0.5 Present (WFH)";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                }

                                results.FooterDetails.PresentHours += halfDayMinutes;
                                /* leavePayableHours = halfDayMinutes;*/
                                unpaidLeavePayableHours += halfDayMinutes;
                                presentPayableHours = halfDayMinutes;
                            }

                            // Case 3: WFH hours less than 4 hours
                            else
                            {
                                if (wfhMinutes < 4 * 60) // WFH less than 4 hours
                                {
                                    status = "Absent";
                                    results.FooterDetails.Absent += payableHoursLimit;
                                }

                                // Case 4: WFH hours between 4 and 9 hours
                                else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60)
                                {
                                    double presentHours = wfhMinutes / 60.0;
                                    presentPayableHours = Convert.ToInt32(presentHours * 60); // Convert hours to minutes

                                    status = "0.5 P (WFH) | 0.5 A";
                                    results.FooterDetails.PresentHours += presentPayableHours;
                                    results.FooterDetails.Absent += halfDayMinutes;
                                }

                                // Case 5: WFH hours greater than or equal to 9 hours (Full Day WFH)
                                else
                                {
                                    status = "Present (WFH)";
                                    results.FooterDetails.PresentHours += payableHoursLimit;
                                    presentPayableHours = payableHoursLimit;
                                }
                            }
                        }

                        //else if (leaves != null && wfh != null) // Check for both WFH and Leave on the same day
                        //{
                        //    int halfDayMinutes = 270;
                        //    var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;

                        //    if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                        //    {
                        //        status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf) Leave and 0.5 Present (WFH)";
                        //        results.FooterDetails.PaidLeave += halfDayMinutes;
                        //        results.FooterDetails.PresentHours += halfDayMinutes;
                        //        leavePayableHours = halfDayMinutes;
                        //        presentPayableHours = halfDayMinutes;
                        //    }
                        //    else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                        //    {
                        //        status = leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) Leave and 0.5 Present (WFH)";
                        //        results.FooterDetails.PaidLeave += halfDayMinutes;
                        //        results.FooterDetails.PresentHours += halfDayMinutes;
                        //        leavePayableHours = halfDayMinutes;
                        //        presentPayableHours = halfDayMinutes;
                        //    }
                        //    else
                        //    {
                        //        // Existing logic for WFH
                        //        if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                        //        {
                        //            status = "Absent";
                        //            results.FooterDetails.Absent += payableHoursLimit;

                        //        }
                        //        else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for WFH hours between 4 and 9 hours
                        //        {
                        //            // Calculate present hours dynamically based on WFH minutes
                        //            double presentHours = wfhMinutes / 60.0;
                        //            presentPayableHours = Convert.ToInt32(presentHours * 60);

                        //            status = "0.5 P (WFH) | 0.5 A";
                        //            results.FooterDetails.PresentHours += presentPayableHours;
                        //            results.FooterDetails.Absent += halfDayMinutes;

                        //        }
                        //        else
                        //        {
                        //            status = "Present (WFH)";
                        //            results.FooterDetails.PresentHours += payableHoursLimit;
                        //            presentPayableHours = payableHoursLimit;
                        //        }
                        //    }
                        //}
                        else if (date < DateTime.Now.Date && date >= joinDate)
                        {
                            status = "Absent";
                            results.FooterDetails.Absent += (payableHoursLimit);

                        }

                        // Set status to empty if it's the current date
                        if (date == DateTime.Now.Date && holiday == null && leaves == null
                                && (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday))
                        {
                            status = "";
                        }

                        AttendanceDetails attendanceDetails = new AttendanceDetails()
                        {
                            AttendanceDate = date,
                            Status = status,
                            PermissionHours = permissionPayableHours,
                            PayableHours = leavePayableHours + presentPayableHours + weekendPayableHours + holidayPayableHours + unpaidLeavePayableHours
                        };
                        results.FooterDetails.PayableHours += (leavePayableHours + presentPayableHours + weekendPayableHours + holidayPayableHours);
                        results.AttendanceDetails.Add(attendanceDetails);
                    }
                }
                else
                {
                    for (DateTime date = fromDate; date <= toDate; date = date.AddDays(1))
                    {
                        var existingEntry = dbList.FirstOrDefault(o => o.Date == date.Date);

                        if (existingEntry == null) // If the date is missing, create a dummy entry
                        {
                            string status = "";
                            int leavePayableHours = 0;
                            int presentPayableHours = 0;
                            int weekendPayableHours = 0;
                            int holidayPayableHours = 0;
                            int unpaidLeavePayableHours = 0;

                            Holiday holiday = _holidayRepository.GetAll().FirstOrDefault(h => h.Date.Date == date);
                            LeaveDetail leaves = _leaveDetailRepository
                                .GetAll()
                                .Include(l => l.LeaveFk.LeaveTypeFk)
                                .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                            PermissionRequest wfh = _permissionRequestRepository
                                .GetAll()
                                .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                                .Where(x => x.PermissionOn.Value.Date == date)
                                .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                                .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                .FirstOrDefault();

                            if ((date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) && holiday == null)
                            {
                                status = "Weekend";
                                results.FooterDetails.Weekend += (payableHoursLimit);
                                weekendPayableHours = payableHoursLimit;
                            }
                            else if (holiday != null)
                            {
                                status = holiday.HolidayName + " (Holiday)";
                                results.FooterDetails.Holidays += (payableHoursLimit);
                                holidayPayableHours = payableHoursLimit;
                            }
                            else if (leaves != null && wfh == null)
                            {
                                int halfDayMinutes = 270; // Half-day in minutes
                                bool isFutureDate = date.Date > DateTime.Now.Date;

                                // Fetch both HalfDayFirstHalf and HalfDaySecondHalf leaves for the same date
                                var halfDayLeaves = _leaveDetailRepository
                                    .GetAll()
                                    .Include(l => l.LeaveFk)
                                    .ThenInclude(l => l.LeaveTypeFk)
                                    .Where(l => l.CreatorUserId == long.Parse(input.UserNameFilter)
                                    && l.Date.Value.Date == date.Date
                                    && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved
                                    && (l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf || l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf))
                                    .ToList();

                                // Case: Both first half and second half leave exist (could be mixed LOP and Paid Leave)
                                if (halfDayLeaves.Count == 2)
                                {
                                    var firstHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                    var secondHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                    var statusParts = new List<string>();

                                    // Check and process the first half leave
                                    if (firstHalfLeave != null)
                                    {
                                        string firstHalfLeaveType = firstHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "FirstHalf Leave Type Missing";
                                        if (firstHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // First half is LOP
                                        {
                                            statusParts.Add($"LOP ( FirstHalf)");
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            /*leavePayableHours = halfDayMinutes;*/
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }
                                        else // First half is Paid Leave
                                        {
                                            statusParts.Add($"{firstHalfLeaveType} (FirstHalf)");
                                            results.FooterDetails.PaidLeave += halfDayMinutes;
                                            leavePayableHours += halfDayMinutes;
                                        }
                                    }

                                    // Check and process the second half leave
                                    if (secondHalfLeave != null)
                                    {
                                        string secondHalfLeaveType = secondHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "SecondHalf Leave Type Missing";
                                        if (secondHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // Second half is LOP
                                        {
                                            statusParts.Add($"LOP ( SecondHalf)");
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            /*leavePayableHours += halfDayMinutes;*/
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }
                                        else // Second half is Paid Leave
                                        {
                                            statusParts.Add($"{secondHalfLeaveType} (SecondHalf)");
                                            results.FooterDetails.PaidLeave += halfDayMinutes;
                                            leavePayableHours += halfDayMinutes;
                                        }
                                    }

                                    // Combine the status parts
                                    status = string.Join("  ", statusParts);
                                }

                                else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Check if the leave is unpaid
                                {
                                    if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf) // LOP for first half
                                    {
                                        halfDayMinutes = 270;
                                        if (halfDayMinutes == 270)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) Leave and 0.5 Absent";
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            /*leavePayableHours = halfDayMinutes;*/
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }

                                    }
                                    else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                    {
                                        halfDayMinutes = 270;
                                        if (halfDayMinutes == 270)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) Leave and 0.5 Absent ";
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes; // Add to unpaid leave for second half
                                            /*leavePayableHours = halfDayMinutes;*/ // Track unpaid hours
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }

                                    }
                                    else // Full day LOP
                                    {
                                        status = " LOP ";
                                        results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                        /*leavePayableHours = payableHoursLimit;*/
                                        unpaidLeavePayableHours += payableHoursLimit;
                                    }
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && date.Date > DateTime.Now.Date)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " (FirstHalf) Leave";
                                    leavePayableHours = halfDayMinutes;
                                    results.FooterDetails.PaidLeave += halfDayMinutes;

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && date.Date > DateTime.Now.Date)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) Leave";
                                    leavePayableHours = halfDayMinutes;
                                    results.FooterDetails.PaidLeave += halfDayMinutes;

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf) Leave and 0.5 Absent";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                    results.FooterDetails.Absent += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) Leave 0.5 Absent";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                    results.FooterDetails.Absent += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                    results.FooterDetails.PaidLeave += payableHoursLimit;
                                    leavePayableHours = payableHoursLimit;
                                }

                            }

                            else if (leaves != null && wfh != null) // Check for both WFH and Leave on the same day
                            {
                                int halfDayMinutes = 270;
                                var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;

                                // Case 1: Half-day Leave (First Half) and sufficient WFH time
                                if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                                {
                                    if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // LOP for first half
                                    {
                                        status = "LOP ( FirstHalf) and 0.5 Present (WFH)";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                    }
                                    else // Paid leave for first half
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + " (FirstHalf) Leave and 0.5 Present (WFH)";
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                    }

                                    results.FooterDetails.PresentHours += halfDayMinutes;
                                    /*leavePayableHours = halfDayMinutes;*/
                                    unpaidLeavePayableHours += halfDayMinutes;
                                    presentPayableHours = halfDayMinutes;
                                }

                                // Case 2: Half-day Leave (Second Half) and sufficient WFH time
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                                {
                                    if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // LOP for second half
                                    {
                                        status = "LOP ( SecondHalf) and 0.5 Present (WFH)";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                    }
                                    else // Paid leave for second half
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + " (SecondHalf) Leave and 0.5 Present (WFH)";
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                    }

                                    results.FooterDetails.PresentHours += halfDayMinutes;
                                    /* leavePayableHours = halfDayMinutes;*/
                                    unpaidLeavePayableHours += halfDayMinutes;
                                    presentPayableHours = halfDayMinutes;
                                }

                                // Case 3: WFH hours less than 4 hours
                                else
                                {
                                    if (wfhMinutes < 4 * 60) // WFH less than 4 hours
                                    {
                                        status = "Absent";
                                        results.FooterDetails.Absent += payableHoursLimit;
                                    }

                                    // Case 4: WFH hours between 4 and 9 hours
                                    else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60)
                                    {
                                        double presentHours = wfhMinutes / 60.0;
                                        presentPayableHours = Convert.ToInt32(presentHours * 60); // Convert hours to minutes

                                        status = "0.5 P (WFH) | 0.5 A";
                                        results.FooterDetails.PresentHours += presentPayableHours;
                                        results.FooterDetails.Absent += halfDayMinutes;
                                    }

                                    // Case 5: WFH hours greater than or equal to 9 hours (Full Day WFH)
                                    else
                                    {
                                        status = "Present (WFH)";
                                        results.FooterDetails.PresentHours += payableHoursLimit;
                                        presentPayableHours = payableHoursLimit;
                                    }
                                }
                            }



                            //else if (leaves != null && wfh != null) // Check for both WFH and Leave on the same day
                            //{
                            //    int halfDayMinutes = 270;
                            //    var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;

                            //    if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                            //    {
                            //        status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf) Leave and 0.5 Present (WFH)";
                            //        results.FooterDetails.PaidLeave += halfDayMinutes;
                            //        results.FooterDetails.PresentHours += halfDayMinutes;
                            //        leavePayableHours = halfDayMinutes;
                            //        presentPayableHours = halfDayMinutes;
                            //    }
                            //    else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                            //    {
                            //        status = leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) Leave and 0.5 Present (WFH)";
                            //        results.FooterDetails.PaidLeave += halfDayMinutes;
                            //        results.FooterDetails.PresentHours += halfDayMinutes;
                            //        leavePayableHours = halfDayMinutes;
                            //        presentPayableHours = halfDayMinutes;
                            //    }
                            //    else
                            //    {
                            //        // Existing logic for WFH
                            //        if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                            //        {
                            //            status = "Absent";
                            //            results.FooterDetails.Absent += payableHoursLimit;

                            //        }
                            //        else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for WFH hours between 4 and 9 hours
                            //        {
                            //            // Calculate present hours dynamically based on WFH minutes
                            //            double presentHours = wfhMinutes / 60.0;
                            //            presentPayableHours = Convert.ToInt32(presentHours * 60);

                            //            status = "0.5 P (WFH) | 0.5 A";
                            //            results.FooterDetails.PresentHours += presentPayableHours;
                            //            results.FooterDetails.Absent += halfDayMinutes; // Assuming 4.5 hours considered absent

                            //        }
                            //        else
                            //        {
                            //            status = "Present (WFH)";
                            //            results.FooterDetails.PresentHours += payableHoursLimit;
                            //            presentPayableHours = payableHoursLimit;
                            //        }
                            //    }
                            //}
                            else if (wfh != null) // Check WFH approval status
                            {
                                var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;
                                if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                                {
                                    status = "Absent";
                                    results.FooterDetails.Absent += payableHoursLimit;

                                }
                                else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for WFH hours between 4 and 9 hours
                                {
                                    // Calculate present hours dynamically based on WFH minutes
                                    double presentHours = wfhMinutes / 60.0;
                                    presentPayableHours = Convert.ToInt32(presentHours * 60);

                                    if (wfhMinutes == 4 * 60)
                                    {
                                        presentPayableHours = ((int)(4.5 * 60)); // 4 hours of WFH results in 4.5 hours present
                                    }

                                    status = "0.5 P (WFH) | 0.5 A";
                                    results.FooterDetails.PresentHours += presentPayableHours;
                                    results.FooterDetails.Absent += 270; // Assuming 4.5 hours considered absent

                                }
                                else
                                {
                                    status = "Present (WFH)";
                                    results.FooterDetails.PresentHours += payableHoursLimit;
                                    presentPayableHours = payableHoursLimit;
                                }
                            }

                            else if (date < DateTime.Now.Date && date >= joinDate)
                            {
                                status = "Absent";
                                results.FooterDetails.Absent += (payableHoursLimit);

                            }
                            // Set status to empty if it's the current date
                            if (date == DateTime.Now.Date && holiday == null && leaves == null
                                && (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday))
                            {
                                status = "";
                            }

                            AttendanceDetails attendanceDetails = new AttendanceDetails()
                            {
                                AttendanceDate = date,
                                PayableHours = leavePayableHours + presentPayableHours + weekendPayableHours + holidayPayableHours + unpaidLeavePayableHours,

                                Status = status
                            };
                            results.FooterDetails.PayableHours += (leavePayableHours + weekendPayableHours + presentPayableHours + holidayPayableHours);
                            results.AttendanceDetails.Add(attendanceDetails);
                        }
                        else
                        {
                            //var firstIn = existingEntry.FirstCheckin?.Eventtime;
                            //var lastout = existingEntry.LastCheckout?.Eventtime;
                            //var punchTotalfirst = 0;
                            //var punchTotalsecond = 0;
                            // changes has remove the seconds in checkin and checkout
                            var firstIn = existingEntry.FirstCheckin?.Eventtime.Date.AddHours(existingEntry.FirstCheckin.Eventtime.Hour)
                                                   .AddMinutes(existingEntry.FirstCheckin.Eventtime.Minute);
                            var lastout = existingEntry.LastCheckout?.Eventtime.Date.AddHours(existingEntry.LastCheckout.Eventtime.Hour)
                                                                              .AddMinutes(existingEntry.LastCheckout.Eventtime.Minute);
                            string status = "";

                            var breakTimeStart = new DateTime(date.Year, date.Month, date.Day, 13, 0, 0);
                            var breakTimeEnd = new DateTime(date.Year, date.Month, date.Day, 14, 0, 0);
                            var allPunches = await _attendanceRepository.GetAll()
                                            .Where(e => e.UserId == long.Parse(input.UserNameFilter))
                                            .Where(e => e.Eventtime.Date == date.Date)
                                            .OrderBy(e => e.Eventtime).Select(e => e.Eventtime.ToLocalTime()).ToListAsync();

                            var allPunchs = allPunches.GroupBy(e => new
                            { e.Year, e.Month, e.Day, e.Hour, e.Minute })
                            .Select(g => g.First())
                            .OrderBy(e => e)
                            .ToList();

                            /*   if (allPunchs.Any() && allPunchs.Count() % 2 == 0)
                               {
                                   for (int i = 0; i < allPunchs.Count(); i += 2)
                                   {
                                       if (Math.Round(allPunchs[i].TimeOfDay.TotalMinutes, 3) <= Math.Round(breakTimeStart.TimeOfDay.TotalMinutes, 3)
                                           && Math.Round(allPunchs[i + 1].TimeOfDay.TotalMinutes, 3) <= Math.Round(breakTimeStart.TimeOfDay.TotalMinutes, 2))
                                       {
                                           punchTotalfirst += Convert.ToInt32(((allPunchs[i + 1].TimeOfDay - allPunchs[i].TimeOfDay) * 60).TotalHours);
                                       }
                                       else if (Math.Round(allPunchs[i].TimeOfDay.TotalMinutes, 3) >= Math.Round(breakTimeStart.TimeOfDay.TotalMinutes, 3)
                                           && Math.Round(allPunchs[i + 1].TimeOfDay.TotalMinutes, 3) >= Math.Round(breakTimeStart.TimeOfDay.TotalMinutes, 2))
                                       {
                                           punchTotalsecond += Convert.ToInt32(((allPunchs[i + 1].TimeOfDay - allPunchs[i].TimeOfDay) * 60).TotalHours);
                                       }
                                   }
                               }*/
                            /*int totalHours = punchTotalfirst + punchTotalsecond + paidBreak;*/


                            int totalHours = Convert.ToInt32(((lastout?.TimeOfDay - firstIn?.TimeOfDay) * 60)?.TotalHours);
                            //int totalHours = (int)Convert.ToDouble(((lastout?.TimeOfDay - firstIn?.TimeOfDay) * 60)?.TotalHours);

                            int TotalMinutes = (int)Convert.ToDouble(((lastout?.TimeOfDay - firstIn?.TimeOfDay))?.TotalMinutes);


                            //int totalHours = Convert.ToInt32(((lastout?.TimeOfDay - firstIn?.TimeOfDay) * 60)?.TotalHours);
                            int totalHoursWithoutBreak = totalHours - paidBreak;
                            Double permissionHours = 0.0;
                            Holiday holiday = null;
                            LeaveDetail leaves = null;
                            Double wfh = 0.0;
                            if (firstIn != null)

                            {
                                permissionHours = (await _permissionRequestRepository.GetAllListAsync())
                               .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                               .Where(x => x.PermissionOn.Value.Date == firstIn.Value.Date)
                               .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                               .Where(x => x.PermissionType == CustomEnum.PermissionType.Permission)
                               .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                               .Sum();


                                holiday = _holidayRepository.GetAll().FirstOrDefault(h => h.Date.Date == firstIn.Value.Date);
                                leaves = _leaveDetailRepository
                               .GetAll()
                               .Include(l => l.LeaveFk.LeaveTypeFk)
                               .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == firstIn.Value.Date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                                wfh = (await _permissionRequestRepository.GetAllListAsync())
                               .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                               .Where(x => x.PermissionOn.Value.Date == firstIn.Value.Date)
                               .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                               .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                               .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                               .Sum();
                            }

                            int originalPayHours = Convert.ToInt32(permissionHours);
                            int payableHours = 0;
                            int LOPHours = 0;

                            if (firstIn != null)
                            {
                                if ((firstIn.Value.DayOfWeek == DayOfWeek.Saturday || firstIn.Value.DayOfWeek == DayOfWeek.Sunday) && holiday == null)
                                {
                                    status = "Weekend";
                                    if (firstIn != null && lastout != null)
                                    {
                                        if (totalHours >= payableHoursLimit && firstIn != null)
                                        {
                                            results.FooterDetails.PresentHours += (payableHoursLimit);
                                            payableHours += (payableHoursLimit);
                                        }
                                        else
                                        {
                                            results.FooterDetails.PresentHours += 270;
                                            payableHours += (totalHours);
                                            results.FooterDetails.Weekend += 270;
                                        }
                                    }
                                    else
                                    {
                                        results.FooterDetails.Weekend += (payableHoursLimit);
                                        payableHours += payableHoursLimit;
                                    }
                                }

                                else if (holiday != null && firstIn != null)
                                {
                                    status = holiday.HolidayName + " (Holiday)";
                                    results.FooterDetails.Holidays += (payableHoursLimit);
                                    payableHours += payableHoursLimit;
                                }
                                else if ((leaves != null) && (permissionHours != 0 || wfh > 0) && (firstIn != null))
                                {
                                    int halfDayMinutes = 270; // Common for half-day leaves
                                    int totalOfficeMinutes = (int)(lastout.Value - firstIn.Value).TotalMinutes;
                                    if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == false) // Paid leave
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                            results.FooterDetails.PaidLeave += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                            permissionHours = wfh + permissionHours;
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) (0.5 Present)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                payableHours += payableHoursLimit;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                        (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                        (totalOfficeMinutes + wfh + halfDayMinutes >= 540))

                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) (0.5 Absent)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.Absent += halfDayMinutes;
                                                payableHours += (totalHours + halfDayMinutes);
                                                permissionHours = wfh + permissionHours;
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (SecondHalf) (0.5 Present)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += ((totalHours + halfDayMinutes) >= originalPayHours ? payableHoursLimit : (totalHours + halfDayMinutes));
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                        (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                       (totalOfficeMinutes + wfh + halfDayMinutes >= 540))
                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (SecondHalf) (0.5 Absent)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.Absent += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                payableHours += (int)(totalHours + halfDayMinutes + permissionHours);

                                            }
                                        }
                                    }
                                    else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Unpaid leave (LOP)
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) Leave and 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                       (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "LOP 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                      (totalOfficeMinutes + wfh + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "LOP 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) Leave";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) Leave and 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                       (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "LOP 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                      (totalOfficeMinutes + wfh + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "LOP 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) Leave";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                        }
                                        else // Full day unpaid leave (LOP)
                                        {
                                            status = "LOP";
                                            results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                            LOPHours += payableHoursLimit;
                                        }
                                    }

                                    totalHoursWithoutBreak = totalHours; // Store the total hours without break
                                }

                                else if (leaves != null)
                                {
                                    int halfDayMinutes = 270; // Common for half-day leaves

                                    if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == false) // Paid leave
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                            results.FooterDetails.PaidLeave += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) (0.5 Present)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                            }

                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) (0.5 Absent)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.Absent += halfDayMinutes;
                                                payableHours += (totalHours + halfDayMinutes);
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (SecondHalf) (0.5 Present)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += ((totalHours + halfDayMinutes) >= originalPayHours ? payableHoursLimit : (totalHours + halfDayMinutes));
                                            }

                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (SecondHalf) (0.5 Absent)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.Absent += halfDayMinutes;
                                                payableHours += (totalHours + halfDayMinutes);
                                            }
                                        }
                                    }
                                    else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Unpaid leave (LOP)
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) Leave and 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                LOPHours += 270;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) Leave";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                LOPHours += 270;
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) Leave and 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                LOPHours += 270;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) Leave";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                LOPHours += 270;
                                            }
                                        }
                                        else // Full day unpaid leave (LOP)
                                        {
                                            status = "LOP";
                                            results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                            LOPHours += payableHoursLimit;
                                        }
                                    }

                                    totalHoursWithoutBreak = totalHours; // Store the total hours without break
                                }

                                /*  else if (wfh > payableHours && lastout != null) // Show WFH for past, current, and future dates
                                  {
                                      if (wfh >= payableHours)
                                      {
                                          status = "Present (WFH)";
                                          results.FooterDetails.PresentHours += payableHoursLimit;
                                          payableHours += payableHoursLimit; // Assuming 240 minutes for WFH
                                      }                       
                                      else
                                      {
                                          status = "Present (WFH)";
                                          results.FooterDetails.PresentHours += (payableHoursLimit);
                                          payableHours += (240 + paidBreak);
                                      }

                                  }*/

                                else if (wfh > 0 && lastout != null && firstIn != null) // Show WFH for past, current, and future dates
                                {
                                    if (wfh > 0 && lastout != null && firstIn != null)
                                    {
                                        // Convert times to minutes for easier calculation
                                        int totalOfficeMinutes = (int)(lastout.Value - firstIn.Value).TotalMinutes;

                                        // Ensure total time including WFH is at least 480 minutes
                                        if (totalOfficeMinutes + wfh >= 540 || totalOfficeMinutes + wfh + permissionHours >= 540 && firstIn != null)
                                        {
                                            status = "Present";
                                            results.FooterDetails.PresentHours += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                            totalHours = payableHours - paidBreak;
                                            permissionHours = wfh + permissionHours;
                                        }

                                        else if (totalHours < 240 && firstIn != null)
                                        {
                                            status = "0.5 P (WFH) | 0.5 Absent";
                                            results.FooterDetails.PresentHours += 270; // Assuming 270 is for half-day present
                                            results.FooterDetails.Absent += 270; // Assuming 270 is for half-day absent
                                            payableHours += 270;
                                        }
                                        else if ((totalOfficeMinutes > 270 && firstIn != null) && permissionHours < 270)
                                        {
                                            status = "0.5 P  | 0.5 A";
                                            results.FooterDetails.PresentHours += 270; // Assuming 270 is for half-day present
                                            results.FooterDetails.Absent += 270; // Assuming 270 is for half-day absent
                                            payableHours = (int)(totalHours + (wfh + permissionHours));
                                            permissionHours = wfh + permissionHours;
                                        }
                                        else
                                        {
                                            status = "Present (WFH)";
                                            results.FooterDetails.PresentHours += (payableHoursLimit); // Full day present
                                            payableHours += payableHoursLimit;
                                        }
                                    }
                                    else if (wfh >= 240 && firstIn != null) // WFH for first half
                                    {
                                        if (totalHours < 240 && firstIn != null)
                                        {
                                            status = "0.5 P (WFH) | 0.5 Absent";
                                            results.FooterDetails.PresentHours += 270; // Assuming 270 is for half-day present
                                            results.FooterDetails.Absent += 270; // Assuming 270 is for half-day absent
                                            payableHours += 270; // Only half-day payable hours since less than 4 hours worked
                                        }
                                        else
                                        {
                                            status = "Present (WFH)";
                                            results.FooterDetails.PresentHours += (payableHoursLimit); // Full day present
                                            payableHours += payableHoursLimit;
                                        }
                                    }
                                    else // WFH for second half
                                    {
                                        if (totalHours < 240 && firstIn != null)
                                        {
                                            status = "0.5 A | 0.5 P (WFH)";
                                            results.FooterDetails.Absent += 270; // Assuming 270 is for half-day absent
                                            results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                            results.FooterDetails.PresentHours += 270; // Assuming 270 is for half-day present
                                            payableHours += 270; // Only half-day payable hours since less than 4 hours worked
                                        }
                                        else
                                        {
                                            status = "Present (WFH)";
                                            results.FooterDetails.PresentHours += payableHoursLimit; // Full day present
                                            payableHours += (240 + paidBreak);
                                        }
                                    }
                                }
                                else if (TotalMinutes < 1)
                                {
                                    status = "Absent";
                                    results.FooterDetails.Absent += payableHoursLimit;
                                }


                                else if ((totalHours != 0) && date.Date != DateTime.Now.Date
                                        && leaves == null && permissionHours == 0 && holiday == null
                                        && firstIn.Value.DayOfWeek != DayOfWeek.Saturday && firstIn.Value.DayOfWeek != DayOfWeek.Sunday
                                        && firstIn != null)
                                {

                                    if (totalHours >= payableHoursLimit && firstIn != null)
                                    {
                                        status = "Present";
                                        results.FooterDetails.PresentHours += (payableHoursLimit);
                                        payableHours += (240 + 240 + paidBreak);
                                    }
                                    else if (totalHours >= 270 && firstIn != null)
                                    {
                                        status = "0.5 P | 0.5 A";
                                        results.FooterDetails.Absent += (270);
                                        //results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                        results.FooterDetails.PresentHours += (270);
                                        payableHours += (totalHours);
                                    }
                                    /*else if (totalHours >= 240)
                                    {
                                        status = "0.5 P | 0.5 A";
                                        results.FooterDetails.Absent += (270);
                                        results.FooterDetails.PresentHours += (270);
                                        payableHours += (totalHours);
                                    }*/
                                    else
                                    {
                                        status = "Absent";
                                        payableHours += (totalHours < 60 ? totalHours + paidBreak : totalHours);
                                        results.FooterDetails.Absent += payableHoursLimit;
                                        results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                    }
                                }
                                else if (date.Date != DateTime.Now.Date
                                        && leaves == null && permissionHours != 0 && holiday == null
                                        && firstIn.Value.DayOfWeek != DayOfWeek.Saturday && firstIn.Value.DayOfWeek != DayOfWeek.Sunday
                                        && firstIn != null)
                                {
                                    totalHours += originalPayHours;
                                    if (totalHours >= payableHoursLimit)
                                    {
                                        status = "Present";
                                        results.FooterDetails.PresentHours += (payableHoursLimit);
                                        payableHours += (240 + 240 + paidBreak);
                                    }
                                    else if (totalHours >= 270 && firstIn != null)
                                    {
                                        status = "0.5 P , 0.5 A";
                                        results.FooterDetails.Absent += (270);
                                        results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                        results.FooterDetails.PresentHours += (270);
                                        payableHours += (totalHours);
                                    }
                                    else
                                    {
                                        status = "Absent";
                                        payableHours += (totalHours < 60 ? totalHours + paidBreak : totalHours);
                                        results.FooterDetails.Absent += payableHoursLimit;
                                    }
                                }
                                else if (totalHours == 0 && lastout == null && firstIn != null)
                                {
                                    status = "Absent";
                                    results.FooterDetails.Absent += payableHoursLimit;

                                }
                            }
                            else
                            {
                                status = "Absent";
                                results.FooterDetails.Absent += payableHoursLimit;
                            }

                            if (date.Date == DateTime.Now.Date && (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday) && holiday == null)
                            {
                                if (leaves == null && permissionHours == 0)
                                {
                                    status = "";
                                    totalHoursWithoutBreak = Convert.ToInt32(((lastout?.TimeOfDay - firstIn?.TimeOfDay) * 60)?.TotalHours);
                                    payableHours += (totalHoursWithoutBreak + paidBreak);
                                }
                            }

                            AttendanceDetails attendanceDetails = new AttendanceDetails()
                            {
                                AttendanceDate = firstIn != null ? firstIn : lastout,
                                FirstIn = firstIn != null ? firstIn : null,
                                LastOut = lastout,
                                TotalHours = totalHoursWithoutBreak < 0 ? totalHours : totalHoursWithoutBreak,
                                PermissionHours = permissionHours,
                                PayableHours = payableHours,
                                BreakHours = paidBreak,
                                Status = status,
                                WFH = payableHours
                            };

                            if (firstIn != null)
                            {


                                if (payableHours >= payableHoursLimit)
                                {
                                    results.FooterDetails.PayableHours += payableHoursLimit;
                                }
                                else if (payableHours <= 240)
                                {
                                    if ((firstIn.Value.DayOfWeek == DayOfWeek.Saturday || firstIn.Value.DayOfWeek == DayOfWeek.Sunday))
                                    {
                                        results.FooterDetails.PayableHours += payableHoursLimit;

                                    }
                                    else
                                    {
                                        results.FooterDetails.PayableHours += 0;
                                    }

                                }
                                else
                                {
                                    results.FooterDetails.PayableHours += 270;
                                }

                                if (LOPHours > 0)
                                {
                                    results.FooterDetails.PayableHours -= LOPHours;
                                }
                            }
                            results.AttendanceDetails.Add(attendanceDetails);
                        }
                    }
                }

                // Re-check leave entries for past "Absent" statuses
                foreach (var detail in results.AttendanceDetails.Where(d => d.Status == "Absent"))
                {
                    LeaveDetail leaves = _leaveDetailRepository
                        .GetAll()
                        .Include(l => l.LeaveFk.LeaveTypeFk)
                        .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == detail.AttendanceDate.Value.Date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                    Double wfh = (await _permissionRequestRepository.GetAllListAsync())
                        .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                        .Where(x => x.PermissionOn.Value.Date == detail.AttendanceDate.Value.Date)
                        .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                        .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                        .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                        .Sum();

                    if (leaves != null)
                    {
                        detail.Status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                        results.FooterDetails.Absent -= payableHoursLimit;
                        results.FooterDetails.PaidLeave += payableHoursLimit;
                    }
                    else if (wfh >= payableHoursLimit)
                    {
                        detail.Status = "Present (WFH)";
                        results.FooterDetails.Absent -= payableHoursLimit;
                        results.FooterDetails.PresentHours += payableHoursLimit;
                    }
                }
                return results;
            }

            catch (Exception e)
            {

                Logger.Log(LogSeverity.Error, $"An error occured while deleting audit logs on host database", e);
                // Log the exception or handle it as needed
                return results;
            }
        }
        public virtual async Task<AttendanceListViewDto> GetAllCustomListView(
       GetAllAttendancesInput input
   )
        {
            var results = new AttendanceListViewDto();
            int paidBreak = 60;
            int payableHoursLimit = 540;
            int halfDayHours = 270;
            try
            {


                var filteredAttendances = _attendanceRepository
                     .GetAll()
                     .Include(e => e.UserFk)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), e => e.EmployeeId.Contains(input.Filter) || e.SourceType.Contains(input.Filter))
                     .WhereIf(input.MinCheckInFilter != null, e => e.CheckIn >= input.MinCheckInFilter)
                     .WhereIf(input.MaxCheckInFilter != null, e => e.CheckIn <= input.MaxCheckInFilter)
                     .WhereIf(input.MinCheckOutFilter != null, e => e.CheckOut >= input.MinCheckOutFilter)
                     .WhereIf(input.MaxCheckOutFilter != null, e => e.CheckOut <= input.MaxCheckOutFilter)
                     .WhereIf(input.MinTotalMinutesFilter != null, e => e.TotalMinutes >= input.MinTotalMinutesFilter)
                     .WhereIf(input.MaxTotalMinutesFilter != null, e => e.TotalMinutes <= input.MaxTotalMinutesFilter)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.EmployeeIdFilter), e => e.EmployeeId.Contains(input.EmployeeIdFilter))
                     .WhereIf(input.MinEventtimeFilter != null, e => e.Eventtime >= input.MinEventtimeFilter)
                     .WhereIf(input.MaxEventtimeFilter != null, e => e.Eventtime <= input.MaxEventtimeFilter)
                     .WhereIf(input.IscheckinFilter.HasValue && input.IscheckinFilter > -1, e => (input.IscheckinFilter == 1 && e.Ischeckin) || (input.IscheckinFilter == 0 && !e.Ischeckin))
                     .WhereIf(input.MinDownloaddateFilter != null, e => e.Downloaddate >= input.MinDownloaddateFilter)
                     .WhereIf(input.MaxDownloaddateFilter != null, e => e.Downloaddate <= input.MaxDownloaddateFilter)
                     .WhereIf(!string.IsNullOrWhiteSpace(input.SourceTypeFilter), e => e.SourceType.Contains(input.SourceTypeFilter))
                     .WhereIf(!string.IsNullOrWhiteSpace(input.UserNameFilter), e => e.UserFk != null && e.UserFk.Id == long.Parse(input.UserNameFilter))
                     .WhereIf(input.FromDate != null && input.ToDate != null, e => e.Eventtime.Date >= input.FromDate.Value.Date && e.Eventtime.Date <= input.ToDate.Value.Date)
                     .WhereIf(input.Month != null, e => e.Eventtime.Date.Year == input.Month.Value.Year && e.Eventtime.Date.Month == input.Month.Value.Month)
                     .GroupBy(e => e.Eventtime.Date)
                     .Select(group => new
                     {
                         Date = group.Key,
                         /* FirstCheckin = group.FirstOrDefault(e => e.Ischeckin == true),
                          LastCheckout = group.OrderByDescending(e => e.Eventtime).FirstOrDefault(e => e.Ischeckin == false)*/
                         // Use regularized check-in if active, otherwise use first regular check-in
                         FirstCheckin = group.FirstOrDefault(e => e.Ischeckin == true && e.RegularizationActive == true)
                        ?? group.FirstOrDefault(e => e.Ischeckin == true),

                         // Use regularized check-out if active, otherwise use last regular check-out
                         LastCheckout = group.OrderByDescending(e => e.Eventtime)
                        .FirstOrDefault(e => e.Ischeckin == false && e.RegularizationActive == true)
                            ?? group.OrderByDescending(e => e.Eventtime)
                         .FirstOrDefault(e => e.Ischeckin == false)
                     });

                int totalCount = await filteredAttendances.CountAsync();
                var dbList = await filteredAttendances.ToListAsync();

                results.FooterDetails = new FooterDetails
                {
                    PayableHours = 0,
                    PresentHours = 0,
                    PaidLeave = 0,
                    UnPaidLeave = 0,
                    Weekend = 0,
                    Holidays = 0,
                    Absent = 0
                };
                results.attendanceDetailsForList = new List<AttendanceDetailForList>();
                DateTime fromDate = new DateTime();
                DateTime toDate = new DateTime();
                if (input.FromDate != null && input.ToDate != null)
                {
                    fromDate = (DateTime)input.FromDate;
                    toDate = (DateTime)input.ToDate;
                }
                else
                {
                    fromDate = new DateTime(input.Month.Value.Year, input.Month.Value.Month, 1);
                    toDate = fromDate.AddMonths(1).AddDays(-1);
                }
                // Get the user's join date
                var user = _userDetailRepository.GetAll().FirstOrDefault(u => u.UserId == long.Parse(input.UserNameFilter));
                var joinDate = user?.DateOfJoin;

                if (dbList.Count == 0 || dbList == null)
                {
                    for (DateTime date = fromDate; date <= toDate; date = date.AddDays(1))
                    {
                        string status = "";
                        int leavePayableHours = 0;
                        int presentPayableHours = 0;
                        int permissionPayableHours = 0;
                        int unpaidLeavePayableHours = 0;
                        Holiday holiday = _holidayRepository.GetAll().FirstOrDefault(h => h.Date.Date == date.Date);
                        LeaveDetail leaves = _leaveDetailRepository
                            .GetAll()
                            .Include(l => l.LeaveFk.LeaveTypeFk)
                            .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == date.Date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                        var permissionWFH = await _permissionRequestRepository
                                .GetAll()
                                .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                                .Where(x => x.PermissionOn.Value.Date == date.Date)
                                .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome || x.PermissionType == CustomEnum.PermissionType.Permission)
                                .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                .ToListAsync();

                        PermissionRequest wfh = permissionWFH.Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome).FirstOrDefault();
                        PermissionRequest permission = permissionWFH.Where(x => x.PermissionType == CustomEnum.PermissionType.Permission).FirstOrDefault();
                        int halfDayMinutes = 270;
                        if (holiday != null)
                        {
                            status = holiday.HolidayName + "(Holiday)";
                            results.FooterDetails.Holidays += (payableHoursLimit);
                        }
                        else if ((date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) && holiday == null)
                        {
                            status = "Weekend";
                            results.FooterDetails.Weekend += (payableHoursLimit);
                        }
                        else if (leaves != null && wfh == null)
                        {
                            bool isFutureDate = date.Date > DateTime.Now.Date;
                            var halfDayLeaves = _leaveDetailRepository
                                .GetAll()
                                .Include(l => l.LeaveFk)
                                .ThenInclude(l => l.LeaveTypeFk)
                                .Where(l => l.CreatorUserId == long.Parse(input.UserNameFilter)
                                && l.Date.Value.Date == date.Date
                                && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved
                                && (l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf || l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf))
                                .ToList();

                            // Case: Both first half and second half leave exist (could be mixed LOP and Paid Leave)
                            if (halfDayLeaves.Count == 2)
                            {
                                var firstHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                var statusParts = new List<string>();

                                // Check and process the first half leave
                                if (firstHalfLeave != null)
                                {
                                    string firstHalfLeaveType = firstHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "FirstHalf Leave Type Missing";
                                    if (firstHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // First half is LOP
                                    {
                                        statusParts.Add($"LOP (FirstHalf)");
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        unpaidLeavePayableHours = halfDayMinutes;
                                    }
                                    else // First half is Paid Leave
                                    {
                                        statusParts.Add($"{firstHalfLeaveType}Leave (FirstHalf)");
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                        leavePayableHours = halfDayMinutes;
                                    }
                                }

                                // Check and process the second half leave
                                if (secondHalfLeave != null)
                                {
                                    string secondHalfLeaveType = secondHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "SecondHalf Leave Type Missing";
                                    if (secondHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // Second half is LOP
                                    {
                                        statusParts.Add($"LOP (SecondHalf)");
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        unpaidLeavePayableHours += halfDayMinutes;
                                    }
                                    else // Second half is Paid Leave
                                    {
                                        statusParts.Add($"{secondHalfLeaveType}Leave (SecondHalf)");
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                        leavePayableHours += halfDayMinutes;
                                    }
                                }

                                // Combine the status parts
                                status = string.Join(" @ ", statusParts);
                            }

                            else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Check if the leave is unpaid
                            {
                                if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf) // LOP for first half
                                {
                                    halfDayMinutes = 270;
                                    if (halfDayMinutes == 270)
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf) & 0.5 Absent";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        unpaidLeavePayableHours += halfDayMinutes;
                                    }

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    halfDayMinutes = 270;
                                    if (halfDayMinutes == 270)
                                    {
                                        status = "0.5 Absent & " + leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf)";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes; // Add to unpaid leave for second half
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        unpaidLeavePayableHours += halfDayMinutes;
                                    }

                                }
                                else // Full day LOP
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name;
                                    results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                    unpaidLeavePayableHours = payableHoursLimit;
                                }
                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && date.Date > DateTime.Now.Date)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave" + "(FirstHalf)";
                                leavePayableHours = halfDayMinutes;
                                results.FooterDetails.PaidLeave += halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && date.Date > DateTime.Now.Date)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave" + "(SecondHalf)";
                                leavePayableHours = halfDayMinutes;
                                results.FooterDetails.PaidLeave += halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + "Leave (FirstHalf) & 0.5 Absent";
                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                results.FooterDetails.Absent += halfDayMinutes;
                                leavePayableHours = halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                            {
                                status = "0.5 Absent & " + leaves.LeaveFk.LeaveTypeFk.Name + "Leave (SecondHalf)";
                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                results.FooterDetails.Absent += halfDayMinutes;
                                leavePayableHours = halfDayMinutes;

                            }
                            else if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                            {
                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                results.FooterDetails.PaidLeave += payableHoursLimit;
                                leavePayableHours = payableHoursLimit;
                            }

                        }

                        else if (wfh != null && leaves == null) // Check WFH approval status
                        {
                            var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;
                            if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                            {
                                status = "Absent";
                                results.FooterDetails.Absent += payableHoursLimit;
                            }

                            else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for cancelled WFH requests
                            {
                                // Calculate present hours dynamically based on WFH minutes
                                double presentHours = wfhMinutes / 60.0;
                                presentPayableHours = Convert.ToInt32(presentHours * 60);

                                if (wfhMinutes == 4 * 60)
                                {
                                    presentPayableHours = ((int)(4.5 * 60)); // 4 hours of WFH results in 4.5 hours present
                                }
                                if (wfh.FromTime.Value.TimeOfDay >= new TimeSpan(13, 0, 0)) // Check if WFH starts after 1 PM
                                {
                                    status = "0.5 Absent # 0.5 Present (WFH)";
                                    results.FooterDetails.PresentHours += presentPayableHours;
                                    results.FooterDetails.Absent += 270; // 4.5 hours considered absent
                                }
                                else
                                {
                                    status = "0.5 Present (WFH) # 0.5 Absent";
                                    results.FooterDetails.PresentHours += presentPayableHours;
                                    results.FooterDetails.Absent += 270; // 4.5 hours considered absent
                                }
                            }
                            /*  else if (wfh.FromTime.Value.TimeOfDay >= new TimeSpan(13, 0, 0)) // Check for second half WFH after 1 PM
                              {
                                  status = "0.5 Absent # 0.5 Present (WFH)";
                                  results.FooterDetails.PresentHours += presentPayableHours;
                                  results.FooterDetails.Absent += 270; // 4.5 hours considered absent
                              }*/
                            else
                            {
                                status = "Present (WFH)";
                                results.FooterDetails.PresentHours += payableHoursLimit;
                                presentPayableHours = payableHoursLimit;
                            }
                        }
                        else if (permission != null)
                        {
                            var permissionMinutes = ((permission.ToTime.Value.TimeOfDay - permission.FromTime.Value.TimeOfDay) * 60).TotalHours;
                            status = "";
                            permissionPayableHours += Convert.ToInt32(permissionMinutes);

                        }
                        else if (leaves != null && wfh != null) // Check WFH approval status
                        {

                            var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;
                            if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == false)
                            {
                                if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "Leave (FirstHalf) # 0.5 Present (WFH)";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                    results.FooterDetails.PresentHours += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;
                                    presentPayableHours = halfDayMinutes;
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                                {
                                    status = "0.5 Present (WFH) # " + leaves.LeaveFk.LeaveTypeFk.Name + "Leave (SecondHalf)";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                    results.FooterDetails.PresentHours += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;
                                    presentPayableHours = halfDayMinutes;
                                }
                            }
                            else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true)
                            {
                                if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf) and 0.5 Present (WFH)";
                                    results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                    results.FooterDetails.PresentHours += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;
                                    presentPayableHours = halfDayMinutes;
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                                {
                                    status = "0.5 Present (WFH) and " + leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) ";
                                    results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                    results.FooterDetails.PresentHours += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;
                                    presentPayableHours = halfDayMinutes;
                                }
                            }

                            else
                            {
                                // Existing logic for WFH
                                if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                                {
                                    status = "Absent";
                                    results.FooterDetails.Absent += payableHoursLimit;
                                }
                                else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for WFH hours between 4 and 9 hours
                                {
                                    // Calculate present hours dynamically based on WFH minutes
                                    double presentHours = wfhMinutes / 60.0;
                                    presentPayableHours = Convert.ToInt32(presentHours * 60);

                                    status = "0.5 Present (WFH) # 0.5 Absent";
                                    results.FooterDetails.PresentHours += presentPayableHours;
                                    results.FooterDetails.Absent += halfDayMinutes; // Assuming 4.5 hours considered absent
                                }
                                else if (wfh.FromTime.Value.TimeOfDay >= TimeSpan.FromHours(13)) // Assuming 12 PM as the start of the second half
                                {
                                    status = "0.5 Absent # 0.5 Present (WFH)";
                                    results.FooterDetails.PresentHours += presentPayableHours;
                                    results.FooterDetails.Absent += halfDayMinutes; // Assuming 4.5 hours considered absent
                                }
                                else
                                {
                                    status = "Present (WFH)";
                                    results.FooterDetails.PresentHours += payableHoursLimit;
                                    presentPayableHours = payableHoursLimit;
                                }
                            }
                        }
                        else if (date < DateTime.Now.Date && date >= joinDate)
                        {
                            status = "Absent";
                            results.FooterDetails.Absent += (payableHoursLimit);
                        }

                        // Set status to empty if it's the current date
                        if (date == DateTime.Now.Date && holiday == null && leaves == null
                                && (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday))
                        {
                            status = "";
                        }

                        var allCheckIn = await _attendanceRepository.GetAll().Where(x => x.Eventtime.Date == date && x.Ischeckin == true && x.UserId == long.Parse(input.UserNameFilter))
                                .OrderBy(x => x.Eventtime)
                                .Select(x => x.Eventtime).ToListAsync();
                        var allCheckOut = await _attendanceRepository.GetAll().Where(x => x.Eventtime.Date == date && x.Ischeckin == false && x.UserId == long.Parse(input.UserNameFilter))
                            .OrderBy(x => x.Eventtime)
                            .Select(x => x.Eventtime).ToListAsync();

                        AttendanceDetailForList attendanceDetails = new AttendanceDetailForList()
                        {
                            Date = date,
                            AllCheckIn = allCheckIn,
                            AllCheckOut = allCheckOut,
                            Status = status
                        };
                        results.attendanceDetailsForList.Add(attendanceDetails);
                    }
                }
                else
                {
                    for (DateTime date = fromDate.Date; date <= toDate.Date; date = date.AddDays(1))
                    {
                        var existingEntry = dbList.FirstOrDefault(o => o.Date == date.Date);

                        if (existingEntry == null) // If the date is missing, create a dummy entry
                        {
                            string status = "";
                            int leavePayableHours = 0;
                            int presentPayableHours = 0;
                            int weekendPayableHours = 0;
                            int holidayPayableHours = 0;
                            int unpaidLeavePayableHours = 0;
                            Holiday holiday = _holidayRepository.GetAll().FirstOrDefault(h => h.Date.Date == date);
                            LeaveDetail leaves = _leaveDetailRepository
                                .GetAll()
                                .Include(l => l.LeaveFk.LeaveTypeFk)
                                .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                            PermissionRequest wfh = _permissionRequestRepository
                                .GetAll()
                                .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                                .Where(x => x.PermissionOn.Value.Date == date)
                                .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                                .FirstOrDefault();

                            if ((date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday) && holiday == null)
                            {
                                status = "Weekend";
                                results.FooterDetails.Weekend += (payableHoursLimit);
                            }
                            else if (holiday != null)
                            {
                                status = holiday.HolidayName + "(Holiday)";
                                results.FooterDetails.Holidays += (payableHoursLimit);
                            }
                            else if (leaves != null && wfh == null)
                            {
                                int halfDayMinutes = 270; // Half-day in minutes
                                bool isFutureDate = date.Date > DateTime.Now.Date;

                                // Fetch both HalfDayFirstHalf and HalfDaySecondHalf leaves for the same date
                                var halfDayLeaves = _leaveDetailRepository
                                    .GetAll()
                                    .Include(l => l.LeaveFk)
                                    .ThenInclude(l => l.LeaveTypeFk)
                                    .Where(l => l.CreatorUserId == long.Parse(input.UserNameFilter)
                                    && l.Date.Value.Date == date.Date
                                    && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved
                                    && (l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf || l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf))
                                    .ToList();

                                // Case: Both first half and second half leave exist (could be mixed LOP and Paid Leave)
                                if (halfDayLeaves.Count == 2)
                                {
                                    var firstHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                    var secondHalfLeave = halfDayLeaves.FirstOrDefault(l => l.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                    var statusParts = new List<string>();

                                    // Check and process the first half leave
                                    if (firstHalfLeave != null)
                                    {
                                        string firstHalfLeaveType = firstHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "FirstHalf Leave Type Missing";
                                        if (firstHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // First half is LOP
                                        {
                                            statusParts.Add($"LOP (FirstHalf)");
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }
                                        else // First half is Paid Leave
                                        {
                                            statusParts.Add($"{firstHalfLeaveType}Leave (FirstHalf)");
                                            results.FooterDetails.PaidLeave += halfDayMinutes;
                                            leavePayableHours += halfDayMinutes;
                                        }
                                    }

                                    // Check and process the second half leave
                                    if (secondHalfLeave != null)
                                    {
                                        string secondHalfLeaveType = secondHalfLeave.LeaveFk?.LeaveTypeFk?.Name ?? "SecondHalf Leave Type Missing";
                                        if (secondHalfLeave.LeaveFk?.LeaveTypeFk?.IsCountLimit == true) // Second half is LOP
                                        {
                                            statusParts.Add($"LOP (SecondHalf)");
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }
                                        else // Second half is Paid Leave
                                        {
                                            statusParts.Add($"{secondHalfLeaveType}Leave (SecondHalf)");
                                            results.FooterDetails.PaidLeave += halfDayMinutes;
                                            leavePayableHours += halfDayMinutes;
                                        }
                                    }

                                    // Combine the status parts
                                    status = string.Join(" @ ", statusParts);
                                }

                                else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Check if the leave is unpaid
                                {
                                    if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf) // LOP for first half
                                    {
                                        halfDayMinutes = 270;
                                        if (halfDayMinutes == 270)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf)  & 0.5 Absent";
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }
                                    }
                                    else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                    {
                                        halfDayMinutes = 270;
                                        if (halfDayMinutes == 270)
                                        {
                                            status = "0.5 Absent & " + leaves.LeaveFk.LeaveTypeFk.Name + "(SecondHalf) ";
                                            results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                            results.FooterDetails.Absent += halfDayMinutes; // Add to unpaid leave for second half
                                            unpaidLeavePayableHours += halfDayMinutes;
                                        }
                                    }
                                    else // Full day LOP
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name;
                                        results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                        unpaidLeavePayableHours += payableHoursLimit;
                                    }
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && date.Date > DateTime.Now.Date)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) ";
                                    leavePayableHours = halfDayMinutes;
                                    results.FooterDetails.PaidLeave += halfDayMinutes;

                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && date.Date > DateTime.Now.Date)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "Leave(SecondHalf) ";
                                    leavePayableHours = halfDayMinutes;
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + "Leave (FirstHalf)  & 0.5 Absent";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                    results.FooterDetails.Absent += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    status = "0.5 Absent & " + leaves.LeaveFk.LeaveTypeFk.Name + "Leave (SecondHalf)";
                                    results.FooterDetails.PaidLeave += halfDayMinutes;
                                    results.FooterDetails.Absent += halfDayMinutes;
                                    leavePayableHours = halfDayMinutes;
                                }
                                else if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                {
                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                    results.FooterDetails.PaidLeave += payableHoursLimit;
                                    leavePayableHours = payableHoursLimit;
                                }
                            }
                            else if (leaves == null && wfh != null)
                            {
                                int halfDayMinutes = 270; // Assuming half-day is 4.5 hours (270 minutes)
                                var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;

                                if (wfh.FromTime.Value.TimeOfDay >= TimeSpan.FromHours(13)) // Second half WFH (starting after 1 PM)
                                {
                                    if (wfhMinutes >= 4 * 60) // WFH duration is 4 hours or more
                                    {
                                        status = "0.5 Absent # 0.5 Present (WFH)";
                                        results.FooterDetails.PresentHours += halfDayMinutes;
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        presentPayableHours = halfDayMinutes;
                                    }
                                    else // WFH duration is less than 4 hours
                                    {
                                        status = "Absent";
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        presentPayableHours = 0;
                                    }
                                }
                                else // First half WFH
                                {
                                    if (wfhMinutes >= 9 * 60) // WFH hours 9 or more
                                    {
                                        status = "Present (WFH)";
                                        results.FooterDetails.PresentHours += payableHoursLimit;
                                        presentPayableHours = payableHoursLimit;
                                    }
                                    else if (wfhMinutes >= 4 * 60) // WFH duration is 4 hours or more
                                    {
                                        status = "0.5 Present (WFH) # 0.5 Absent";
                                        results.FooterDetails.PresentHours += halfDayMinutes;
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        presentPayableHours = halfDayMinutes;

                                    }
                                    else // WFH duration is less than 4 hours
                                    {
                                        status = "Absent";
                                        results.FooterDetails.Absent += halfDayMinutes;
                                        presentPayableHours = 0;
                                    }

                                }

                            }

                            else if (leaves != null && wfh != null) // Check WFH approval status
                            {
                                int halfDayMinutes = 270;
                                var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;
                                if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == false)
                                {
                                    if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + "Leave (FirstHalf) # 0.5 Present (WFH)";
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                        results.FooterDetails.PresentHours += halfDayMinutes;
                                        leavePayableHours = halfDayMinutes;
                                        presentPayableHours = halfDayMinutes;
                                    }
                                    else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                                    {
                                        status = "0.5 Present (WFH) # " + leaves.LeaveFk.LeaveTypeFk.Name + "Leave (SecondHalf) ";
                                        results.FooterDetails.PaidLeave += halfDayMinutes;
                                        results.FooterDetails.PresentHours += halfDayMinutes;
                                        leavePayableHours = halfDayMinutes;
                                        presentPayableHours = halfDayMinutes;
                                    }
                                }
                                else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true)
                                {
                                    if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf && wfhMinutes >= 4 * 60)
                                    {
                                        status = leaves.LeaveFk.LeaveTypeFk.Name + "(FirstHalf)  and  0.5 Present (WFH)";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        results.FooterDetails.PresentHours += halfDayMinutes;
                                        leavePayableHours = halfDayMinutes;
                                        presentPayableHours = halfDayMinutes;
                                    }
                                    else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf && wfhMinutes >= 4 * 60)
                                    {
                                        status = "0.5 Present (WFH)  and " + leaves.LeaveFk.LeaveTypeFk.Name + " (SecondHalf) ";
                                        results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                        results.FooterDetails.PresentHours += halfDayMinutes;
                                        leavePayableHours = halfDayMinutes;
                                        presentPayableHours = halfDayMinutes;
                                    }
                                }

                                else
                                {
                                    // Existing logic for WFH
                                    if (wfhMinutes < 4 * 60) // Check for WFH hours less than 4 hours
                                    {
                                        status = "Absent";
                                        results.FooterDetails.Absent += payableHoursLimit;
                                    }
                                    else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // Check for WFH hours between 4 and 9 hours
                                    {
                                        // Calculate present hours dynamically based on WFH minutes
                                        double presentHours = wfhMinutes / 60.0;
                                        presentPayableHours = Convert.ToInt32(presentHours * 60);

                                        status = "0.5 Present (WFH) # 0.5 Absent";
                                        results.FooterDetails.PresentHours += presentPayableHours;
                                        results.FooterDetails.Absent += halfDayMinutes; // Assuming 4.5 hours considered absent
                                    }
                                    else if (wfh.FromTime.Value.TimeOfDay >= TimeSpan.FromHours(13)) // Assuming 12 PM as the start of the second half
                                    {
                                        status = "0.5 Absent # 0.5 Present (WFH)";
                                        results.FooterDetails.PresentHours += presentPayableHours;
                                        results.FooterDetails.Absent += halfDayMinutes; // Assuming 4.5 hours considered absent
                                    }
                                    else
                                    {
                                        status = "Present (WFH)";
                                        results.FooterDetails.PresentHours += payableHoursLimit;
                                        presentPayableHours = payableHoursLimit;
                                    }
                                }
                            }
                            else if (wfh != null) // Check WFH approval status
                            {
                                var wfhMinutes = (wfh.ToTime.Value.TimeOfDay - wfh.FromTime.Value.TimeOfDay).TotalMinutes;

                                if (wfhMinutes < 4 * 60) // WFH hours less than 4 hours
                                {
                                    status = "Absent";
                                    results.FooterDetails.Absent += payableHoursLimit;
                                }
                                else if (wfhMinutes >= 9 * 60) // WFH hours 9 or more
                                {
                                    status = "Present (WFH)";
                                    results.FooterDetails.PresentHours += payableHoursLimit;
                                    presentPayableHours = payableHoursLimit;
                                }
                                else if (wfhMinutes >= 4 * 60 && wfhMinutes < 9 * 60) // WFH hours between 4 and 9 hours
                                {
                                    // Calculate present hours dynamically based on WFH minutes
                                    double presentHours = wfhMinutes / 60.0;
                                    presentPayableHours = Convert.ToInt32(presentHours * 60);

                                    if (wfhMinutes == 4 * 60)
                                    {
                                        presentPayableHours = ((int)(4.5 * 60)); // 4 hours of WFH results in 4.5 hours present
                                    }

                                    // Check if WFH is in the second half of the day
                                    if (wfh.FromTime.Value.TimeOfDay >= TimeSpan.FromHours(13)) // Assuming 12 PM as the start of the second half
                                    {
                                        status = "0.5 Absent # 0.5 Present (WFH)";
                                        results.FooterDetails.PresentHours += presentPayableHours;
                                        results.FooterDetails.Absent += 270; // Assuming 4.5 hours considered absent
                                    }
                                    else
                                    {
                                        status = "0.5 Present (WFH) # 0.5 Absent";
                                        results.FooterDetails.PresentHours += presentPayableHours;
                                        results.FooterDetails.Absent += 270; // Assuming 4.5 hours considered absent
                                    }
                                }
                                /*else // WFH hours 9 or more
                                {
                                    status = "Present (WFH)";
                                    results.FooterDetails.PresentHours += payableHoursLimit;
                                    presentPayableHours = payableHoursLimit;
                                }*/
                            }
                            else if (date < DateTime.Now.Date && date >= joinDate)
                            {
                                status = "Absent";
                                results.FooterDetails.Absent += (payableHoursLimit);
                            }
                            // Set status to empty if it's the current date
                            if (date == DateTime.Now.Date && holiday == null && leaves == null
                                && (date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday))
                            {
                                status = "";
                            }

                            var allCheckIn = await _attendanceRepository.GetAll().Where(x => x.Eventtime.Date == date && x.Ischeckin == true && x.UserId == long.Parse(input.UserNameFilter))
                                .OrderBy(x => x.Eventtime)
                                .Select(x => x.Eventtime).ToListAsync();
                            var allCheckOut = await _attendanceRepository.GetAll().Where(x => x.Eventtime.Date == date && x.Ischeckin == false && x.UserId == long.Parse(input.UserNameFilter))
                                .OrderBy(x => x.Eventtime)
                                .Select(x => x.Eventtime).ToListAsync();

                            AttendanceDetailForList attendanceDetails = new AttendanceDetailForList()
                            {
                                Date = date,
                                Status = status,
                                AllCheckIn = allCheckIn,
                                AllCheckOut = allCheckOut
                            };

                            results.FooterDetails.PayableHours += (leavePayableHours + weekendPayableHours + presentPayableHours + holidayPayableHours);
                            results.attendanceDetailsForList.Add(attendanceDetails);
                        }
                        else
                        {
                            /* var firstIn = existingEntry.FirstCheckin?.Eventtime;
                             var lastout = existingEntry.LastCheckout?.Eventtime;*/

                            var firstIn = existingEntry.FirstCheckin?.Eventtime.Date.AddHours(existingEntry.FirstCheckin.Eventtime.Hour)
                                                   .AddMinutes(existingEntry.FirstCheckin.Eventtime.Minute);
                            var lastout = existingEntry.LastCheckout?.Eventtime.Date.AddHours(existingEntry.LastCheckout.Eventtime.Hour)
                                                                              .AddMinutes(existingEntry.LastCheckout.Eventtime.Minute);

                            int totalHours = Convert.ToInt32(((lastout?.TimeOfDay - firstIn?.TimeOfDay) * 60)?.TotalHours);
                            int TotalMinutes = (int)Convert.ToDouble(((lastout?.TimeOfDay - firstIn?.TimeOfDay))?.TotalMinutes);
                            int totalHoursWithoutBreak = totalHours - paidBreak;
                            int payableHours = 0;
                            int LOPHours = 0;
                            var allCheckIn = await _attendanceRepository.GetAll().Where(x => x.Eventtime.Date == date && x.Ischeckin == true && x.UserId == long.Parse(input.UserNameFilter))
                               .OrderBy(x => x.Eventtime)
                               .Select(x => x.Eventtime).ToListAsync();
                            var allCheckOut = await _attendanceRepository.GetAll().Where(x => x.Eventtime.Date == date && x.Ischeckin == false && x.UserId == long.Parse(input.UserNameFilter))
                                .OrderBy(x => x.Eventtime)
                                .Select(x => x.Eventtime).ToListAsync();

                            if (firstIn != null)
                            {


                                Double permissionHours = (await _permissionRequestRepository.GetAllListAsync())
                                    .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                                    .Where(x => x.PermissionOn.Value.Date == firstIn.Value.Date)
                                    .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                    .Where(x => x.PermissionType == CustomEnum.PermissionType.Permission)
                                    .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                                    .Sum();

                                int originalPayHours = Convert.ToInt32(permissionHours);
                                Holiday holiday = _holidayRepository.GetAll().FirstOrDefault(h => h.Date.Date == firstIn.Value.Date);
                                string status = "";
                                var breakTimeStart = new DateTime(date.Year, date.Month, date.Day, 13, 0, 0);
                                var breakTimeEnd = new DateTime(date.Year, date.Month, date.Day, 14, 0, 0);
                                var allPunches = await _attendanceRepository.GetAll()
                                                .Where(e => e.UserId == long.Parse(input.UserNameFilter))
                                                .Where(e => e.Eventtime.Date == date.Date)
                                                .OrderBy(e => e.Eventtime).Select(e => e.Eventtime.ToLocalTime()).ToListAsync();

                                LeaveDetail leaves = _leaveDetailRepository
                                    .GetAll()
                                    .Include(l => l.LeaveFk.LeaveTypeFk)
                                    .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == firstIn.Value.Date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);
                                Double wfh = (await _permissionRequestRepository.GetAllListAsync())
                                    .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                                    .Where(x => x.PermissionOn.Value.Date == firstIn.Value.Date)
                                    .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                    .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                                    .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                                    .Sum();

                               
                                if ((firstIn.Value.DayOfWeek == DayOfWeek.Saturday || firstIn.Value.DayOfWeek == DayOfWeek.Sunday) && holiday == null)
                                {
                                    status = "Weekend";
                                    if (firstIn != null && lastout != null)
                                    {
                                        if (totalHours >= payableHoursLimit)
                                        {
                                            results.FooterDetails.PresentHours += (payableHoursLimit);
                                            payableHours += (payableHoursLimit);
                                        }
                                        else
                                        {
                                            results.FooterDetails.PresentHours += 270;
                                            payableHours += (totalHours);
                                            results.FooterDetails.Weekend += 270;
                                        }
                                    }
                                    else
                                    {
                                        results.FooterDetails.Weekend += (payableHoursLimit);
                                        payableHours += payableHoursLimit;
                                    }
                                }
                                else if (totalHours == 0 && lastout == null)
                                {
                                    if (date == DateTime.Now.Date)
                                    {
                                        status = "";
                                    }
                                    else
                                    {
                                        status = "Absent";
                                        results.FooterDetails.Absent += payableHoursLimit;
                                    }
                                }
                                else if (holiday != null)
                                {
                                    status = holiday.HolidayName + "(Holiday)";
                                    results.FooterDetails.Holidays += (payableHoursLimit);
                                    payableHours += payableHoursLimit;
                                }

                                else if ((leaves != null) && (permissionHours != 0 || wfh > 0))
                                {
                                    int halfDayMinutes = 270; // Common for half-day leaves
                                    int totalOfficeMinutes = (int)(lastout.Value - firstIn.Value).TotalMinutes;
                                    if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == false) // Paid leave
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                            results.FooterDetails.PaidLeave += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                            permissionHours = wfh + permissionHours;
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) (0.5 Present)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                payableHours += payableHoursLimit;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                        (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                        (totalOfficeMinutes + wfh + halfDayMinutes >= 540))

                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (FirstHalf) (0.5 Absent)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.Absent += halfDayMinutes;
                                                payableHours += (totalHours + halfDayMinutes);
                                                permissionHours = wfh + permissionHours;
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (SecondHalf) (0.5 Present)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += ((totalHours + halfDayMinutes) >= originalPayHours ? payableHoursLimit : (totalHours + halfDayMinutes));
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                        (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = "0.5 Present / 0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                       (totalOfficeMinutes + wfh + halfDayMinutes >= 540))
                                            {
                                                status = "0.5 " + leaves.LeaveFk.LeaveTypeFk.Name + " Leave / 0.5 Present";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += payableHoursLimit;
                                                permissionHours = wfh + permissionHours;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave (SecondHalf) (0.5 Absent)";
                                                results.FooterDetails.PaidLeave += halfDayMinutes;
                                                results.FooterDetails.Absent += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                payableHours += (int)(totalHours + halfDayMinutes + permissionHours);

                                            }
                                        }
                                    }
                                    else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true) // Unpaid leave (LOP)
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "( FirstHalf)  and 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                       (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                      (totalOfficeMinutes + wfh + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( FirstHalf) ";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null && totalHours >= 240)
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf)  and 0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                       (totalOfficeMinutes + permissionHours + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else if (firstIn != null && lastout != null &&
                                                      (totalOfficeMinutes + wfh + halfDayMinutes >= 540))
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + "0.5 Present";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                results.FooterDetails.PresentHours += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                            else
                                            {
                                                status = leaves.LeaveFk.LeaveTypeFk.Name + " ( SecondHalf) ";
                                                results.FooterDetails.UnPaidLeave += halfDayMinutes;
                                                payableHours += halfDayMinutes;
                                                permissionHours = wfh + permissionHours;
                                                LOPHours += 270;
                                            }
                                        }
                                        else // Full day unpaid leave (LOP)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name;
                                            results.FooterDetails.UnPaidLeave += payableHoursLimit;
                                            payableHours += payableHoursLimit;
                                            LOPHours += payableHoursLimit;
                                        }
                                    }

                                    totalHoursWithoutBreak = totalHours; // Store the total hours without break
                                }
                                else if (leaves != null)
                                {
                                    if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == true)
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name;
                                            results.FooterDetails.UnPaidLeave += (payableHoursLimit);
                                            payableHours += (payableHoursLimit);
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null)
                                            {
                                                if (totalHours >= 240)
                                                {
                                                    /* status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave & " +  "(FirstHalf)" + "(0.5 Present)";*/
                                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " (FirstHalf) & 0.5 Present";
                                                    results.FooterDetails.UnPaidLeave += (270);
                                                    results.FooterDetails.PresentHours += (270);
                                                    payableHours += payableHoursLimit;
                                                }
                                                else
                                                {
                                                    /*status = leaves.LeaveFk.LeaveTypeFk.Name  + " Leave & " + "(FirstHalf)" + "(0.5 Absent)";*/
                                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " (FirstHalf) & 0.5 Absent";
                                                    results.FooterDetails.UnPaidLeave += (270);
                                                    results.FooterDetails.Absent += (270);
                                                    payableHours += (totalHours + 270);
                                                }
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null)
                                            {
                                                if (totalHours >= 240)
                                                {
                                                    status = "(0.5 Present) & " + leaves.LeaveFk.LeaveTypeFk.Name + " (SecondHalf) ";
                                                    results.FooterDetails.UnPaidLeave += (270);
                                                    results.FooterDetails.PresentHours += (270);
                                                    payableHours += ((totalHours + 270) >= originalPayHours ? payableHoursLimit : (totalHours + 270));
                                                }
                                                else
                                                {
                                                    status = "(0.5 Absent) & " + leaves.LeaveFk.LeaveTypeFk.Name + " (SecondHalf) ";
                                                    results.FooterDetails.UnPaidLeave += (270);
                                                    results.FooterDetails.Absent += (270);
                                                    payableHours += (totalHours + 270);
                                                }
                                            }
                                        }
                                    }
                                    else if (leaves.LeaveFk.LeaveTypeFk.IsCountLimit == false)
                                    {
                                        if (leaves.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                        {
                                            status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                                            results.FooterDetails.PaidLeave += (payableHoursLimit);
                                            payableHours += (payableHoursLimit);
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                        {
                                            if (firstIn != null && lastout != null)
                                            {
                                                if (totalHours >= 240)
                                                {
                                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave & " + "(FirstHalf)" + "(0.5 Present)";
                                                    results.FooterDetails.PaidLeave += (270);
                                                    results.FooterDetails.PresentHours += (270);
                                                    payableHours += payableHoursLimit;
                                                }
                                                else
                                                {
                                                    status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave & " + "(FirstHalf)" + "(0.5 Absent)";
                                                    results.FooterDetails.PaidLeave += (270);
                                                    results.FooterDetails.Absent += (270);
                                                    payableHours += (totalHours + 270);
                                                }
                                            }
                                        }
                                        else if (leaves.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                        {
                                            if (firstIn != null && lastout != null)
                                            {
                                                if (totalHours >= 240)
                                                {
                                                    status = "(0.5 Present) & " + leaves.LeaveFk.LeaveTypeFk.Name + "Leave (SecondHalf) ";
                                                    results.FooterDetails.PaidLeave += (270);
                                                    results.FooterDetails.PresentHours += (270);
                                                    payableHours += ((totalHours + 270) >= originalPayHours ? payableHoursLimit : (totalHours + 270));
                                                }
                                                else
                                                {
                                                    status = "(0.5 Absent) & " + leaves.LeaveFk.LeaveTypeFk.Name + "Leave (SecondHalf) ";
                                                    results.FooterDetails.PaidLeave += (270);
                                                    results.FooterDetails.Absent += (270);
                                                    payableHours += (totalHours + 270);
                                                }
                                            }
                                        }

                                    }
                                }
                                else if (wfh > 0 && lastout != null) // Show WFH for past, current, and future dates
                                {
                                    if (wfh >= 240) // WFH for first half
                                    {
                                        if (totalHours < 240)
                                        {
                                            status = "0.5 Present (WFH)  #  0.5 Absent";
                                            results.FooterDetails.PresentHours += 270; // Assuming 270 is for half-day present
                                            results.FooterDetails.Absent += 270; // Assuming 270 is for half-day absent
                                            payableHours += 270; // Only half-day payable hours since less than 4 hours worked
                                        }
                                        else
                                        {
                                            status = "Present (WFH)";
                                            results.FooterDetails.PresentHours += (payableHoursLimit); // Full day present
                                            payableHours += payableHoursLimit;
                                        }
                                    }

                                    else // WFH for second half
                                    {
                                        if (totalHours < 240)
                                        {
                                            status = "0.5 Absent  #  0.5 Present (WFH)";
                                            results.FooterDetails.Absent += 270; // Assuming 270 is for half-day absent
                                            results.FooterDetails.PresentHours += 270; // Assuming 270 is for half-day present
                                            payableHours += 270; // Only half-day payable hours since less than 4 hours worked
                                        }
                                        else
                                        {
                                            status = "Present (WFH)";
                                            results.FooterDetails.PresentHours += payableHoursLimit; // Full day present
                                            payableHours += (240 + paidBreak);
                                        }
                                    }
                                }
                                else if ((totalHours != 0) && date.Date != DateTime.Now.Date
                                             && leaves == null && permissionHours == 0 && holiday == null
                                             && firstIn.Value.DayOfWeek != DayOfWeek.Saturday && firstIn.Value.DayOfWeek != DayOfWeek.Sunday)

                                {
                                    DateTime onePM = new DateTime(date.Year, date.Month, date.Day, 13, 0, 0); // 1 PM
                                    DateTime oneThirtyPM = new DateTime(date.Year, date.Month, date.Day, 13, 30, 0); // 1:30 PM
                                    if (totalHours >= payableHoursLimit)
                                    {
                                        status = "Present";
                                        results.FooterDetails.PresentHours += payableHoursLimit;
                                        payableHours += (240 + 240 + paidBreak);
                                    }
                                    else if (totalHours >= 270)
                                    {
                                        if (firstIn.Value <= oneThirtyPM)
                                        {
                                            status = "0.5 P $ 0.5 A"; // First half present, second half absent
                                            results.FooterDetails.Absent += 270;
                                            results.FooterDetails.PresentHours += 270;
                                        }
                                        else
                                        {
                                            status = "0.5 A $ 0.5 P"; // First half absent, second half present
                                            results.FooterDetails.Absent += 270;
                                            results.FooterDetails.PresentHours += 270;
                                        }
                                        payableHours += totalHours;
                                    }
                                    else
                                    {
                                        status = "Absent";
                                        payableHours += (totalHours < 60 ? totalHours + paidBreak : totalHours);
                                        results.FooterDetails.Absent += payableHoursLimit;
                                    }
                                }
                                else if ((totalHours != 0) && date.Date != DateTime.Now.Date
                                          && leaves == null && permissionHours != 0 && holiday == null
                                          && firstIn.Value.DayOfWeek != DayOfWeek.Saturday && firstIn.Value.DayOfWeek != DayOfWeek.Sunday)
                                {
                                    DateTime onePM = new DateTime(date.Year, date.Month, date.Day, 13, 0, 0); // 1 PM
                                    DateTime oneThirtyPM = new DateTime(date.Year, date.Month, date.Day, 13, 30, 0); // 1:30 PM
                                    if (totalHours >= payableHoursLimit || totalHours + originalPayHours >= payableHoursLimit)
                                    {
                                        status = "Present";
                                        results.FooterDetails.PresentHours += payableHoursLimit;
                                        payableHours += (240 + 240 + paidBreak);
                                        permissionHours = permissionHours;
                                    }

                                    else if (totalHours >= 270)
                                    {
                                        // Check if first check-in is before 1:30 PM
                                        if (firstIn.Value <= oneThirtyPM)
                                        {
                                            status = "0.5 P $ 0.5 A"; // First half present, second half absent
                                            results.FooterDetails.Absent += 270;
                                            results.FooterDetails.PresentHours += 270;
                                        }

                                        else
                                        {
                                            status = "0.5 A $ 0.5 P"; // First half absent, second half present
                                            results.FooterDetails.Absent += 270;
                                            results.FooterDetails.PresentHours += 270;
                                        }
                                        payableHours += totalHours;
                                    }

                                    else
                                    {
                                        status = "Absent";
                                        payableHours += (totalHours < 60 ? totalHours + paidBreak : totalHours);
                                        results.FooterDetails.Absent += payableHoursLimit;
                                    }
                            }
                           
                            AttendanceDetailForList attendanceDetails = new AttendanceDetailForList()
                            {
                                Date = (DateTime)(firstIn != null ? firstIn : lastout),
                                FirstCheckIn = firstIn != null ? firstIn : null,
                                LastCheckOut = lastout,
                                TotalHours = totalHours,
                                PermissionHours = permissionHours,
                                PayableHours = payableHours,
                                BreakHours = paidBreak,
                                Status = status,
                                WFH = payableHours,
                                AllCheckIn = allCheckIn,
                                AllCheckOut = allCheckOut
                            };
                            
                                if (payableHours >= payableHoursLimit)
                                {
                                    results.FooterDetails.PayableHours += payableHoursLimit;
                                }
                                else if (payableHours <= 240)
                                {
                                    if ((firstIn.Value.DayOfWeek == DayOfWeek.Saturday || firstIn.Value.DayOfWeek == DayOfWeek.Sunday))
                                    {
                                        results.FooterDetails.PayableHours += payableHoursLimit;
                                    }
                                    else
                                    {
                                        results.FooterDetails.PayableHours += 0;
                                    }
                                }
                                else
                                {
                                    results.FooterDetails.PayableHours += 270;
                                }
                            
                            results.attendanceDetailsForList.Add(attendanceDetails);
                            }
                            else
                            {
                                AttendanceDetailForList attendanceDetails = new AttendanceDetailForList()
                                {
                                    Date = (DateTime)(firstIn != null ? firstIn : lastout),
                                    FirstCheckIn = firstIn != null ? firstIn : null,
                                    LastCheckOut = lastout,
                                    TotalHours = totalHours,
                                    PermissionHours = 0,
                                    PayableHours = 0,
                                    BreakHours = paidBreak,
                                    Status = "Absent",
                                    WFH = payableHours,
                                    AllCheckIn = allCheckIn,
                                    AllCheckOut = allCheckOut
                                };
                                results.attendanceDetailsForList.Add(attendanceDetails);
                            }
                        }
                    }
                }

                // Re-check leave entries for past "Absent" statuses
                foreach (var detail in results.attendanceDetailsForList.Where(d => d.Status == "Absent"))
                {
                    LeaveDetail leaves = _leaveDetailRepository
                        .GetAll()
                        .Include(l => l.LeaveFk.LeaveTypeFk)
                        .FirstOrDefault(l => l.CreatorUserId == long.Parse(input.UserNameFilter) && l.Date.Value.Date == detail.Date.Date && l.LeaveFk.Status == CustomEnum.LeaveApprovalStatus.Approved);

                    Double wfh = (await _permissionRequestRepository.GetAllListAsync())
                        .Where(x => x.UserId == long.Parse(input.UserNameFilter))
                        .Where(x => x.PermissionOn.Value.Date == detail.Date.Date)
                        .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                        .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                        .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                        .Sum();

                    if (leaves != null)
                    {
                        detail.Status = leaves.LeaveFk.LeaveTypeFk.Name + " Leave";
                        results.FooterDetails.Absent -= payableHoursLimit;
                        results.FooterDetails.PaidLeave += payableHoursLimit;
                    }
                    else if (wfh >= payableHoursLimit)
                    {
                        detail.Status = "Present (WFH)";
                        results.FooterDetails.Absent -= payableHoursLimit;
                        results.FooterDetails.PresentHours += payableHoursLimit;
                    }
                }
                results.FooterDetails.PayableHours = results.FooterDetails.Weekend + results.FooterDetails.Holidays + results.FooterDetails.PaidLeave + results.FooterDetails.PresentHours;
                return results;
            }
            catch (Exception e)
            {

                Logger.Log(LogSeverity.Error, $"An error occured while deleting audit logs on host database", e);
                // Log the exception or handle it as needed
                return results;
            }
        }
        public bool isCheckin(int userId, DateTime date)
        {
            var filteredAttendances = _attendanceRepository
                .GetAll()
                .Where(i => i.UserId == userId)
                .Where(i => i.Eventtime.Date == date.Date)
                .OrderByDescending(i => i.Id) // Assuming Id is a unique identifier or a timestamp field
                .FirstOrDefault();
            // Check if filteredAttendance is not null before accessing its properties
            if (filteredAttendances != null && filteredAttendances.Ischeckin != null)
            {
                return filteredAttendances.Ischeckin;
            }
            return false;
        }
        public isCheckInStatusDto isCheckinStatus(int userId, DateTime date)
        {
            isCheckInStatusDto isCheckInStatus = new isCheckInStatusDto();
            var filteredAttendances = _attendanceRepository
                .GetAll()
                .Where(i => i.UserId == userId)
                .Where(i => i.Eventtime.Date == date.Date)
                .OrderByDescending(i => i.Id) // Assuming Id is a unique identifier or a timestamp field
                .FirstOrDefault();
            // Check if filteredAttendance is not null before accessing its properties
            if (filteredAttendances != null && filteredAttendances.Ischeckin != null)
            {
                isCheckInStatus.isCheckin = filteredAttendances.Ischeckin;
            }
            else
            {
                isCheckInStatus.isFirstCheckIn = true;
            }
            return isCheckInStatus;
        }
        public AttendanceDto GetAllCheckin()
        {
            var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day);
            var filteredAttendances = _attendanceRepository
                .GetAll()
                .Where(e => e.Eventtime.Date == date)
                .Where(e => e.UserId == AbpSession.UserId)
                .Where(e => e.Ischeckin == true)
                .Where(e => e.TenantId == AbpSession.TenantId)
                .FirstOrDefault();
            if (filteredAttendances != null)
            {
                var res = new AttendanceDto()
                {
                    UserId = filteredAttendances.UserId,
                    Eventtime = filteredAttendances.Eventtime
                };
                return res;
            }

            return null;
        }

        // Write your custom code here.
        // ASP.NET Zero Power Tools will not overwrite this class when you regenerate the related entity.

        public virtual async Task CustomCreateOrEdit(CreateOrEditAttendanceDto input)
        {
            if (input.Id == null)
            {
                await CustomCreate(input);
            }
            else
            {
                await CustomUpdate(input);
            }
        }


        public virtual async Task CustomCreateRegularizationMap(CreateOrEditAttendanceDto input, int RegId)
        {
            try
            {


                var attendance = ObjectMapper.Map<Attendance>(input);
                if (AbpSession.TenantId != null)
                {
                    attendance.TenantId = (int?)AbpSession.TenantId;
                }
                var userDetail = _userDetailRepository
                    .GetAll()
                    .Where(u => u.UserId == input.UserId)
                    .FirstOrDefault();
                if (userDetail == null)
                {
                    throw new UserFriendlyException("UserDetails not available, Contact Admin");
                }
                attendance.EmployeeId = userDetail.EmployeeId;
                attendance.RegularizationActive = false;
                attendance.RegularizationId = RegId;
                /*attendance.RegularizationActive = null;*/

                //await _attendanceRepository.InsertAsync(attendance);
            }
            catch (Exception)
            {

                throw new UserFriendlyException("An unexpected error occurred. Please try again later.");
            }
        }
        public virtual async Task CustomCreateAttendancesMap(CreateOrEditAttendanceDto input, int RegId)
        {
            try
            {


                var attendance = ObjectMapper.Map<Attendance>(input);
                if (attendance.TenantId == null)
                {
                    attendance.TenantId = (int?)AbpSession.TenantId;
                }
                var userDetail = _userDetailRepository
                    .GetAll()
                    .Where(u => u.UserId == input.UserId)
                    .FirstOrDefault();
                if (userDetail == null)
                {
                    throw new UserFriendlyException("UserDetails not available, Contact Admin");
                }
                attendance.EmployeeId = userDetail.EmployeeId;
                attendance.RegularizationActive = false;
                attendance.RegularizationId = RegId;
                await _attendanceRepository.InsertAsync(attendance);
            }
            catch (Exception)
            {

                throw new UserFriendlyException("An unexpected error occurred. Please try again later.");
            }
        }
        public virtual async Task CustomCreate(CreateOrEditAttendanceDto input)
        {
            var attendance = ObjectMapper.Map<Attendance>(input);
            if (AbpSession.TenantId != null)
            {
                attendance.TenantId = (int?)AbpSession.TenantId;
            }
            var userDetail = _userDetailRepository
                .GetAll()
                .Where(u => u.UserId == input.UserId)
                .FirstOrDefault();
            if (userDetail == null)
            {
                throw new UserFriendlyException("UserDetails not available, Contact Admin");
            }
            attendance.EmployeeId = userDetail.EmployeeId;

            await _attendanceRepository.InsertAsync(attendance);
        }

        [AbpAuthorize(AppPermissions.Pages_Attendances_Edit)]
        protected virtual async Task CustomUpdate(CreateOrEditAttendanceDto input)
        {
            var attendance = await _attendanceRepository.FirstOrDefaultAsync((int)input.Id);
            ObjectMapper.Map(input, attendance);
        }

        public virtual async Task<List<int>> findAllReportees(List<int> userIds)
        {
            var userDetls = _userDetailsRepository
                .GetAll()
                .Where(e => e.ReporteeId == AbpSession.UserId)
                .ToList();
            if (userDetls.Any())
            {
                userIds = await getAllReportingList(userIds, userDetls);
            }
            return userIds;
        }

        public virtual async Task<List<int>> getAllReportingList(
            List<int> userIds,
            List<UserDetail> userReportingDtls
        )
        {
            foreach (var userDetail in userReportingDtls)
            {
                userIds.Add((int)userDetail.UserId);
                var usrinnerDetails = _userDetailsRepository
                    .GetAll()
                    .Where(e => e.ReporteeId == userDetail.UserId)
                    .ToList();
                if (usrinnerDetails.Any())
                {
                    await getAllReportingList(userIds, usrinnerDetails);
                }
            }
            return userIds;
        }

        public async Task<AttendanceGetAll> CustomGetTeamDetails()
        {
            int? tenantId = AbpSession.TenantId;
            bool fullView = true;
            List<int> userIds = new List<int> { (int)AbpSession.UserId };

            // Check permissions
            if (!PermissionChecker.IsGranted(AppPermissions.Pages_TimesheetReports_FullView) &&
                !PermissionChecker.IsGranted(AppPermissions.Pages_DetailedReports_FullView) &&
                !PermissionChecker.IsGranted(AppPermissions.Pages_DetailedReports_PartialView))
            {
                fullView = false;
                userIds = await findAllReportees(userIds);
            }

            AttendanceGetAll reportData = new AttendanceGetAll();

            // Fetch role IDs
            var managerRole = await _roleRepository.FirstOrDefaultAsync(r => r.Name.ToLower() == "Manager".ToLower());
            var tlRole = await _roleRepository.FirstOrDefaultAsync(r => r.Name.ToLower() == "Team Lead".ToLower());
            var superAdminRole = await _roleRepository.FirstOrDefaultAsync(r => r.Name.ToLower() == "Super Admin".ToLower());
            var adminRole = await _roleRepository.FirstOrDefaultAsync(r => r.Name.ToLower() == "Admin".ToLower());

            int managerRoleId = managerRole != null ? managerRole.Id : 0;
            int tlRoleId = tlRole != null ? tlRole.Id : 0;
            int superAdminRoleId = superAdminRole != null ? superAdminRole.Id : 0;
            int adminRoleId = adminRole != null ? adminRole.Id : 0;

            // Get project SPOC users
            var roleName = await _roleRepository.FirstOrDefaultAsync(e => e.Name.ToLower() == "External Approver".ToLower());
            var prjSpocUserId = (await _userRoleRepository.GetAllListAsync())
                .Where(u => u.TenantId == tenantId && u.RoleId == roleName.Id)
                .Select(r => r.UserId)
                .Distinct()
                .ToList();

            // Get project IDs
            reportData.ProjectIds = (await _projectRepository.GetAllListAsync())
                .Where(u => u.TenantId == tenantId)
                .Select(e => new ProjectGetAllDto
                {
                    ProjectId = e.Id,
                    ProjectName = e.Name ?? ""
                })
                .ToList();

            // Get team IDs
            var allTeams = (await _lookup_userRepository.GetAllListAsync())
                .Where(u => u.TenantId == tenantId && !prjSpocUserId.Contains(u.Id) && u.IsActive == true) // Added IsActive == 1 filter
                .Select(e => new AttendanceTeamsGetAllDto
                {
                    TeamId = (int)e.Id,
                    TeamName = (e.Name ?? "") + " " + (e.Surname ?? ""),
                    EmployeeId = _userDetailRepository
                        .GetAll()
                        .Where(ud => ud.TenantId == tenantId && ud.UserId == e.Id)
                        .Select(ud => ud.EmployeeId)
                        .FirstOrDefault(),
                    UserId = e.Id
                })
                .ToList();

            reportData.TeamIds = allTeams;

            // Get client IDs
            reportData.ClientIds = (await _clientRepository.GetAllListAsync())
                .Where(u => u.TenantId == tenantId)
                .Select(e => new ClientGetAllDto
                {
                    ClentId = e.Id,
                    ClientName = e.Name ?? ""
                })
                .ToList();

            if (!fullView)
            {
                var prjUserMap = _prjUserMapRepository
                    .GetAll()
                    .Where(e => e.TenantId == tenantId && userIds.Contains((int)e.UserId))
                    .ToList();
                var ids = prjUserMap.Select(r => r.ProjectId).Distinct().ToList();
                reportData.ProjectIds = reportData.ProjectIds.Where(e => ids.Contains(e.ProjectId)).ToList();

                reportData.TeamIds = reportData.TeamIds.Where(e => userIds.Contains(e.TeamId)).ToList();

                var clientData = (await _projectRepository.GetAllListAsync())
                    .Where(e => e.TenantId == tenantId && ids.Contains(e.Id));
                var cliList = clientData.Select(r => r.ProjectClient).Distinct().ToList();
                reportData.ClientIds = reportData.ClientIds.Where(e => cliList.Contains(e.ClentId)).ToList();
            }

            reportData.TaskIds = (await _lookup_taskGroupDetailRepository.GetAllListAsync())
                .Where(u => u.TenantId == tenantId)
                .Select(e => new TaskGetAllDto
                {
                    TaskId = e.Id,
                    TaskName = e.Name ?? ""
                })
                .ToList();

            var adminReporting = _lookup_userRepository.GetAll()
                .FirstOrDefault(e => e.Id == AbpSession.UserId);

            if (adminReporting != null)
            {
                var userRoles = await _userRoleRepository.GetAllListAsync();
                var roleIds = userRoles
                    .Where(r => r.UserId == AbpSession.UserId)
                    .Select(r => r.RoleId)
                    .ToList();

                if (roleIds.Contains(superAdminRoleId)) // Super Admin
                {
                    // Super Admin case: all users
                    reportData.TeamIds = allTeams;
                }
                else if (roleIds.Contains(adminRoleId)) // Admin
                {
                    // Admin case: all users
                    reportData.TeamIds = allTeams;
                }
                else if (roleIds.Contains(tlRoleId)) // Team Lead
                {
                    // TL case: direct reportees
                    var reporteeIds = await _userDetailRepository.GetAll()
                        .Where(e => e.ReporteeId == AbpSession.UserId) // Adjust property name as needed
                        .Select(e => e.UserId)
                        .ToListAsync();

                    reportData.TeamIds = allTeams
                        .Where(e => reporteeIds.Contains(e.UserId) || AbpSession.UserId.Equals(e.UserId))
                        .ToList();
                }
                else if (roleIds.Contains(managerRoleId)) // Manager
                {
                    // Manager case: TLs and TLs' reportees
                    var tlIds = await _userDetailRepository.GetAll()
                        .Where(e => e.ReporteeId == AbpSession.UserId) // Adjust property name as needed
                        .Select(e => e.UserId)
                        .ToListAsync();

                    var reporteeIds = await _userDetailRepository.GetAll()
                        .Where(e => tlIds.Contains(e.ReporteeId)) // Adjust property name as needed
                        .Select(e => e.UserId)
                        .ToListAsync();

                    reportData.TeamIds = allTeams
                        .Where(e => tlIds.Contains(e.UserId) || reporteeIds.Contains(e.UserId) || AbpSession.UserId.Equals(e.UserId))
                        .ToList();
                }
            }

            return reportData;
        }

        //AttendanceReportDetails

        [HttpGet]
        public async Task<List<UserAttendanceReport>> GenerateAttendanceReport(GetAttendanceReportInput input)
        {

            int payableHoursLimit = 480;
            int paidBreakHours = 60; // Example value for paid break hours
            int totalPayableHours = payableHoursLimit + paidBreakHours;
            int halfDayThreshold = 270;  // 4 hours in minutes
            int wfhThreshold = 270;      // WFH threshold in minutes
            int halfBreak = 30;


            var userReports = new List<UserAttendanceReport>();
            var activeUsers = await (from user in _userDetailRepository.GetAll()
                                     join lookupUser in _lookup_userRepository.GetAll() on user.UserId equals lookupUser.Id
                                     where user.TenantId == input.TenantId && lookupUser.IsActive
                                     select user).ToListAsync();

            var firstDayOfMonth = new DateTime(input.Year, input.Month, 1);
            var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
            var today = DateTime.Today;

            foreach (var user in activeUsers)
            {
                var username = _lookup_userRepository.GetAll().Where(x => x.Id == user.UserId).Select(x => $"{x.Name} {x.Surname}").FirstOrDefault();
                var EmailId = _lookup_userRepository.GetAll().Where(x => x.Id == user.UserId).Select(x => x.EmailAddress).FirstOrDefault();
                var leaveTypes = _leavetypeRepository.GetAll().ToList();
                var userReport = new UserAttendanceReport
                {
                    EmployeeId = user.EmployeeId,
                    UserName = username,
                    EMailId = EmailId,
                    AttendanceDetails = new List<AttendanceDetail>()
                };

                // Create a dictionary of leave type abbreviations
                var leaveTypeAbbreviations = leaveTypes.ToDictionary(
                    lt => lt.Id,
                    lt => lt.Name switch
                    {
                        "Casual" => "CL",
                        "Sick" => "SL",
                        "Optional" => "OPH",
                        "LOP" => "LOP",
                        _ => lt.Name
                    }
                );

                // Fetch attendance records for the user within the date range
                var attendanceDetails = await _attendanceRepository.GetAll()
                 .Where(e => e.Eventtime.Date >= firstDayOfMonth && e.Eventtime.Date <= lastDayOfMonth && e.UserId == user.UserId)
                 .OrderBy(e => e.Eventtime)
                 .Select(e => new
                 {
                     Eventtime = e.Eventtime.AddSeconds(-e.Eventtime.Second).AddMilliseconds(-e.Eventtime.Millisecond),
                     isCheckin = e.Ischeckin
                 })
                 .ToListAsync();

                // Loop through each day of the month
                for (var date = firstDayOfMonth; date <= lastDayOfMonth; date = date.AddDays(1))
                {
                    var dailyDetails = new AttendanceDetail
                    {
                        Date = date,
                        Status = date < today ? "A" : "-"
                    };

                    if (IsWeekend(date))
                    {
                        dailyDetails.Status = "W"; // Weekend
                    }
                    else if (IsHoliday(date))
                    {
                        dailyDetails.Status = "H"; // Holiday
                    }
                    else
                    {
                        // Fetch approved leave for the user on the specific date
                        var approvedLeave = (from leave in _leaveRepository.GetAll()
                                             join leaveDetail in _leaveDetailRepository.GetAll()
                                             on leave.Id equals leaveDetail.LeaveFk.Id
                                             where leaveDetail.CreatorUserId == user.UserId &&
                                                   leave.FromDate.Date <= date.Date &&
                                                   leave.ToDate.Date >= date.Date &&
                                                   leave.Status == CustomEnum.LeaveApprovalStatus.Approved &&
                                                   (leaveDetail.Date == date.Date)
                                             select new
                                             {
                                                 Leave = leave,
                                                 LeaveDetail = leaveDetail
                                             }).ToList();


                        // Fetch WFH details for the user on the specific date
                        double wfh = (await _permissionRequestRepository.GetAllListAsync())
                            .Where(x => x.UserId == user.UserId)
                            .Where(x => x.PermissionOn.Value.Date == date.Date)
                            .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                            .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                            .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                            .Sum();


                        var dailyAttendances = attendanceDetails
                                 .Where(a => a.Eventtime.Date == date)
                                 .Select(a => new
                                 {
                                     Eventtime = a.Eventtime.AddSeconds(-a.Eventtime.Second).AddMilliseconds(-a.Eventtime.Millisecond),
                                     Ischeckin = a.isCheckin
                                 })
                                 .ToList();




                        if (approvedLeave.Count() > 0 && approvedLeave.Any() && wfh == 0)
                        {
                            foreach (var approvedLeaves in approvedLeave)
                            {
                                var leaveTypeId = approvedLeaves.Leave.LeaveTypeId.GetValueOrDefault();
                                var abbreviation = leaveTypeAbbreviations.TryGetValue(leaveTypeId, out var abbr) ? abbr : "Unknown";
                                var dailyAttendance = attendanceDetails.Where(a => a.Eventtime.Date == date).ToList();
                                var firstCheckin = dailyAttendances.FirstOrDefault(a => a.Ischeckin == true)?.Eventtime;
                                var lastout_new = dailyAttendances
                                .LastOrDefault(a => a.Ischeckin == false)?.Eventtime ?? DateTime.MinValue;


                                var totalMinutes = (firstCheckin != null && lastout_new != DateTime.MinValue)
                                ? (lastout_new - firstCheckin.Value).TotalMinutes
                                : 0;
                                var firstHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                // Get the abbreviation for first half leave
                                var firstHalfAbbreviation = firstHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(firstHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr1) ? abbr1 : "Unknown"
                                    : abbreviation;

                                // Get the abbreviation for second half leave
                                var secondHalfAbbreviation = secondHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(secondHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr2) ? abbr2 : "Unknown"
                                    : abbreviation;

                                // Get permission minutes for the day
                                double permissionMinutes = (await _permissionRequestRepository.GetAllListAsync())
                                    .Where(x => x.UserId == user.UserId)
                                    .Where(x => x.PermissionOn.Value.Date == date.Date)
                                    .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                    .Where(x => x.PermissionType == CustomEnum.PermissionType.Permission)
                                    .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                                    .Sum();

                                // Check if there's no attendance record
                                if (!dailyAttendance.Any())
                                {
                                    // Handle the case where there is no attendance record
                                    if (firstHalfLeave != null && secondHalfLeave != null)
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5{secondHalfAbbreviation}"; // No attendance record for both halves
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                    {

                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";// No attendance record for the second half
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                    {
                                        dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}"; // No attendance record for the first half
                                    }
                                    else
                                    {
                                        dailyDetails.Status = abbreviation; // Default case (could be full day leave or other types)
                                    }
                                }
                                else
                                {
                                    // Handle the case where there is attendance record
                                    if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                    {
                                        dailyDetails.Status = abbreviation; // Full day leave
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                    {
                                        if (totalMinutes + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P";
                                        }
                                        else if (totalMinutes + halfBreak + permissionMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P";
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                        }
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                    {
                                        if (totalMinutes + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}";
                                        }
                                        else if (totalMinutes + halfBreak + permissionMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}";
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                        }
                                    }
                                    else if (firstHalfLeave != null && secondHalfLeave != null)
                                    {

                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5{secondHalfAbbreviation}";// No attendance record for the both halfves
                                    }
                                }
                            }
                        }

                        else if (approvedLeave.Count() > 0 && wfh > 0 && dailyAttendances.Any())
                        {
                            var dailyAttendance = attendanceDetails
                               .Where(a => a.Eventtime.Date == date)
                               .Select(a => new
                               {
                                   Eventtime = a.Eventtime.AddSeconds(-a.Eventtime.Second).AddMilliseconds(-a.Eventtime.Millisecond)
                               })
                               .ToList();
                            var firstCheckin = dailyAttendances.FirstOrDefault(a => a.Ischeckin == true)?.Eventtime;
                            var lastout_new = dailyAttendances
                            .LastOrDefault(a => a.Ischeckin == false)?.Eventtime ?? DateTime.MinValue;


                            var totalMinutes = (firstCheckin != null && lastout_new != DateTime.MinValue)
                            ? (lastout_new - firstCheckin.Value).TotalMinutes
                            : 0;
                            // Leave and WFH overlap on the same day
                            foreach (var approvedLeaves in approvedLeave)
                            {
                                var leaveTypeId = approvedLeaves.Leave.LeaveTypeId.GetValueOrDefault();
                                var abbreviation = leaveTypeAbbreviations.TryGetValue(leaveTypeId, out var abbr) ? abbr : "Unknown";
                                var firstHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                // Get the abbreviation for first half leave
                                var firstHalfAbbreviation = firstHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(firstHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr1) ? abbr1 : "Unknown"
                                    : abbreviation;

                                // Get the abbreviation for second half leave
                                var secondHalfAbbreviation = secondHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(secondHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr2) ? abbr2 : "Unknown"
                                    : abbreviation;



                                // Calculate WFH time for each half
                                var firstHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh;
                                var secondHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh - firstHalfWfh;


                                if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                {
                                    // Full day leave takes precedence
                                    dailyDetails.Status = abbreviation; // Full day leave, regardless of WFH
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                {
                                    if (wfh + halfBreak + totalMinutes >= halfDayThreshold)
                                    {
                                        // First half is leave; check second half for WFH
                                        if (secondHalfWfh + halfBreak + totalMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P"; // Leave first half, WFH second half
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A"; // Leave first half, Absent second half
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A"; // Leave first half, Absent second half
                                    }
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    if (wfh + halfBreak + totalMinutes >= halfDayThreshold)
                                    {
                                        // Second half is leave; check first half for WFH
                                        if (firstHalfWfh + halfBreak + totalMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}"; // WFH first half, Leave second half
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}"; // Absent first half, Leave second half
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}"; // Absent first half, Leave second half
                                    }
                                }
                            }
                        }

                        else if (approvedLeave.Count() > 0 && wfh > 0)
                        {
                            // Leave and WFH overlap on the same day
                            foreach (var approvedLeaves in approvedLeave)
                            {
                                var leaveTypeId = approvedLeaves.Leave.LeaveTypeId.GetValueOrDefault();
                                var abbreviation = leaveTypeAbbreviations.TryGetValue(leaveTypeId, out var abbr) ? abbr : "Unknown";
                                var firstHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);

                                // Get the abbreviation for first half leave
                                var firstHalfAbbreviation = firstHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(firstHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr1) ? abbr1 : "Unknown"
                                    : abbreviation;

                                // Get the abbreviation for second half leave
                                var secondHalfAbbreviation = secondHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(secondHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr2) ? abbr2 : "Unknown"
                                    : abbreviation;



                                // Calculate WFH time for each half
                                var firstHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh;
                                var secondHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh - firstHalfWfh;


                                if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                {
                                    // Full day leave takes precedence
                                    dailyDetails.Status = abbreviation; // Full day leave, regardless of WFH
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                {
                                    if (wfh + halfBreak >= halfDayThreshold)
                                    {
                                        // First half is leave; check second half for WFH
                                        if (secondHalfWfh + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P"; // Leave first half, WFH second half
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A"; // Leave first half, Absent second half
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A"; // Leave first half, Absent second half
                                    }
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    if (wfh + halfBreak >= halfDayThreshold)
                                    {
                                        // Second half is leave; check first half for WFH
                                        if (firstHalfWfh + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}"; // WFH first half, Leave second half
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}"; // Absent first half, Leave second half
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}"; // Absent first half, Leave second half
                                    }
                                }
                            }
                        }

                        else
                        {
                            // Handle the case where there is no leave ,attendance is used to determine status
                            var dailyAttendance = attendanceDetails
                                .Where(a => a.Eventtime.Date == date)
                                .Select(a => new
                                {
                                    Eventtime = a.Eventtime.AddSeconds(-a.Eventtime.Second).AddMilliseconds(-a.Eventtime.Millisecond)
                                })
                                .ToList();
                            var firstCheckin = dailyAttendances.FirstOrDefault(a => a.Ischeckin == true)?.Eventtime;
                            var lastout_new = dailyAttendances
                            .LastOrDefault(a => a.Ischeckin == false)?.Eventtime ?? DateTime.MinValue;


                            var totalMinutes = (firstCheckin != null && lastout_new != DateTime.MinValue)
                            ? (lastout_new - firstCheckin.Value).TotalMinutes
                            : 0;

                            // Get permission minutes for the day
                            double permissionMinutes = (await _permissionRequestRepository.GetAllListAsync())
                                .Where(x => x.UserId == user.UserId)
                                .Where(x => x.PermissionOn.Value.Date == date.Date)
                                .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                .Where(x => x.PermissionType == CustomEnum.PermissionType.Permission)
                                .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                                .Sum();

                            if (wfh > 0 && !dailyAttendance.Any())
                            {
                                if (wfh >= totalPayableHours)
                                {
                                    dailyDetails.Status = "P";
                                }
                                else if (wfh >= wfhThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                }

                            }
                            else if (wfh > 0 && dailyAttendance.Any() && permissionMinutes > 0)
                            {


                                if (totalMinutes + wfh + permissionMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "P";
                                }
                                else if (totalMinutes + wfh + permissionMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                }

                            }
                            else if (wfh > 0 && dailyAttendance.Any())
                            {


                                if (totalMinutes + wfh >= totalPayableHours)
                                {
                                    dailyDetails.Status = "P";
                                }
                                else if (totalMinutes + wfh >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                }

                            }
                            else if (wfh > 0 && permissionMinutes > 0)
                            {


                                if (wfh + permissionMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "P";
                                }
                                else if (wfh + permissionMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                }

                            }

                            else if (dailyAttendance.Any() && permissionMinutes > 0)
                            {


                                if (totalMinutes + permissionMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "P";
                                }
                                else if (totalMinutes + permissionMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                }

                            }

                            else if (dailyAttendance.Any())
                            {
                                if (totalMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "P"; // Full day present without WFH
                                }
                                else if (totalMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A"; // Half day present, half day absent
                                }
                                else
                                {
                                    dailyDetails.Status = "A"; // Absent
                                }
                            }
                            else
                            {
                                dailyDetails.Status = "A"; // Absent
                            }
                        }



                    }

                    userReport.AttendanceDetails.Add(dailyDetails);
                }

                userReports.Add(userReport);
            }

            return userReports;
        }
        private bool IsWeekend(DateTime date)
        {

            return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
        }

        private bool IsHoliday(DateTime date)
        {
            var holidays = _holidayRepository.GetAll().ToList();
            var isHoliday = holidays.Any(h => h.Date.Date == date.Date);


            return isHoliday;
        }

        [HttpGet]
        public async Task<List<UserAttendanceDailyReport>> GenerateDailyAttendanceReport(GetAttendanceDailyReportInput input)
        {
            int payableHoursLimit = 480;
            int paidBreakHours = 60; // Example value for paid break hours
            int totalPayableHours = payableHoursLimit + paidBreakHours;
            int halfDayThreshold = 270;  // 4 hours in minutes
            int wfhThreshold = 270;      // WFH threshold in minutes
            int halfBreak = 30;
            var userReports = new List<UserAttendanceDailyReport>();
            var today = DateTime.Today;
            var activeUsers = await (from user in _userDetailRepository.GetAll()
                                     join lookupUser in _lookup_userRepository.GetAll() on user.UserId equals lookupUser.Id
                                     where user.TenantId == input.TenantId
                                           && lookupUser.IsActive
                                           && user.DateOfJoin <= input.date // User joined on or before the input date
                                           && (user.DateOfRelease == null || user.DateOfRelease >= input.date) // Not released or released after the input date
                                     select user).ToListAsync();
            foreach (var user in activeUsers)
            {
                var username = _lookup_userRepository.GetAll().Where(x => x.Id == user.UserId).Select(x => $"{x.Name} {x.Surname}").FirstOrDefault();
                var EmailId = _lookup_userRepository.GetAll().Where(x => x.Id == user.UserId).Select(x => x.EmailAddress).FirstOrDefault();
                var leaveTypes = _leavetypeRepository.GetAll().ToList();
                var userReport = new UserAttendanceDailyReport
                {
                    EmployeeId = user.EmployeeId,
                    UserName = username,
                    EMailId = EmailId,
                    AttendanceDetails = new List<AttendanceDetail>()
                };
                // Create a dictionary of leave type abbreviations
                var leaveTypeAbbreviations = leaveTypes.ToDictionary(
                    lt => lt.Id,
                    lt => lt.Name switch
                    {
                        "Casual" => "CL",
                        "Sick" => "SL",
                        "Optional" => "OPH",
                        "LOP" => "LOP",
                        _ => lt.Name
                    }
                );
                // Fetch attendance records for the user on the specific date
                var attendanceDetails = await _attendanceRepository.GetAll()
                    .Where(e => e.Eventtime.Date == input.date.Date && e.UserId == user.UserId)
                    .OrderBy(e => e.Eventtime)
                    .Select(e => new
                    {
                        Eventtime = e.Eventtime.AddSeconds(-e.Eventtime.Second).AddMilliseconds(-e.Eventtime.Millisecond),
                        isCheckin = e.Ischeckin
                    })
                    .ToListAsync();
                var dailyDetails = new AttendanceDetail
                {
                    Date = input.date.Date,
                    Status = input.date.Date < today.Date ? "A" : "-"
                };
                if (IsWeekend(input.date.Date))
                {
                    dailyDetails.Status = "Weekend"; // Weekend
                }
                else if (IsHoliday(input.date.Date))
                {
                    dailyDetails.Status = "Holiday"; // Holiday
                }
                else
                {
                    var approvedLeave = (from leave in _leaveRepository.GetAll()
                                         join leaveDetail in _leaveDetailRepository.GetAll()
                                         on leave.Id equals leaveDetail.LeaveFk.Id
                                         where leaveDetail.CreatorUserId == user.UserId &&
                                               leave.FromDate.Date <= input.date.Date &&
                                               leave.ToDate.Date >= input.date.Date &&
                                               leave.Status == CustomEnum.LeaveApprovalStatus.Approved &&
                                               (leaveDetail.Date == input.date.Date)
                                         select new
                                         {
                                             Leave = leave,
                                             LeaveDetail = leaveDetail
                                         }).ToList();
                    double wfh = (await _permissionRequestRepository.GetAllListAsync())
                        .Where(x => x.UserId == user.UserId)
                        .Where(x => x.PermissionOn.Value.Date == input.date.Date)
                        .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                        .Where(x => x.PermissionType == CustomEnum.PermissionType.WorkFromHome)
                        .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                        .Sum();
                    var dailyAttendances = attendanceDetails
                             .Where(a => a.Eventtime.Date == input.date.Date)
                             .Select(a => new
                             {
                                 Eventtime = a.Eventtime.AddSeconds(-a.Eventtime.Second).AddMilliseconds(-a.Eventtime.Millisecond),
                                 Ischeckin = a.isCheckin
                             })
                             .ToList();
                    double permissionMinutes = (await _permissionRequestRepository.GetAllListAsync())
                                .Where(x => x.UserId == user.UserId)
                                .Where(x => x.PermissionOn.Value.Date == input.date.Date)
                                .Where(x => x.Status == CustomEnum.PermissionApprovalStatus.Approved)
                                .Where(x => x.PermissionType == CustomEnum.PermissionType.Permission)
                                .Select(x => (x.ToTime - x.FromTime).Value.TotalMinutes)
                                .Sum();
                    if (input.date.Date == today.Date)
                    {
                        var todayAttendance = attendanceDetails
                            .Where(a => a.Eventtime.Date == today.Date)
                            .OrderBy(a => a.Eventtime)
                            .ToList();
                        if (todayAttendance.Any())
                        {
                            DateTime? firstIn = todayAttendance.FirstOrDefault(a => a.isCheckin)?.Eventtime;
                            DateTime? lastOut = todayAttendance.LastOrDefault(a => !a.isCheckin)?.Eventtime;
                            if (firstIn.HasValue)
                            {
                                dailyDetails.FirstCheckin = firstIn;
                            }
                            if (lastOut.HasValue)
                            {
                                dailyDetails.LastCheckout = lastOut;
                            }
                            if (firstIn.HasValue && lastOut.HasValue)
                            {
                                TimeSpan totalWorked = lastOut.Value - firstIn.Value;
                                dailyDetails.TotalHours = totalWorked;
                            }
                            else
                            {
                                dailyDetails.TotalHours = TimeSpan.Zero;
                            }
                            // Revised Status Logic
                            if (todayAttendance.Last().isCheckin)
                            {
                                // If the last event is a check-in, set status to "In"
                                dailyDetails.Status = "In";
                            }
                            else if (firstIn.HasValue && !lastOut.HasValue)
                            {
                                // If there is a check-in but no check-out
                                dailyDetails.Status = "In";
                            }
                            else if (!firstIn.HasValue && lastOut.HasValue)
                            {
                                // If there is a check-out but no check-in
                                dailyDetails.Status = "Out";
                            }
                            else
                            {
                                dailyDetails.Status = "Out"; // Default status
                            }
                        }
                        else
                        {
                            // Default values if no attendance data
                            dailyDetails.FirstCheckin = null;
                            dailyDetails.LastCheckout = null;
                            dailyDetails.TotalHours = TimeSpan.Zero;
                            dailyDetails.Status = "-";
                        }
                    }
                    else if (input.date.Date < today.Date)
                    {
                        if (approvedLeave.Count() > 0 && approvedLeave.Any() && wfh == 0)
                        {
                            foreach (var approvedLeaves in approvedLeave)
                            {
                                var leaveTypeId = approvedLeaves.Leave.LeaveTypeId.GetValueOrDefault();
                                var abbreviation = leaveTypeAbbreviations.TryGetValue(leaveTypeId, out var abbr) ? abbr : "Unknown";
                                var dailyAttendance = attendanceDetails.Where(a => a.Eventtime.Date == input.date.Date).ToList();
                                var firstCheckin = dailyAttendances.FirstOrDefault(a => a.Ischeckin == true)?.Eventtime;
                                var lastout_new = dailyAttendances.LastOrDefault(a => a.Ischeckin == false)?.Eventtime ?? DateTime.MinValue;

                                double totalMinutes = 0;
                                DateTime? finalCheckout = null;
                                // Ensure there is both a valid check-in and check-out time
                                if (firstCheckin != null && lastout_new != DateTime.MinValue)
                                {
                                    var timeDifference = lastout_new - firstCheckin.Value;
                                    // If the time difference is positive, calculate total minutes
                                    if (timeDifference.TotalMinutes > 0)
                                    {
                                        totalMinutes = timeDifference.TotalMinutes;
                                        finalCheckout = lastout_new;  // Only set final checkout if it is valid
                                    }
                                }
                                // If no valid checkout, set total minutes to 0 and set lastout_new to null or an appropriate placeholder
                                if (lastout_new == DateTime.MinValue)
                                {
                                    totalMinutes = 0;
                                    finalCheckout = null; // You can use null, or set a placeholder like "0" depending on what the view expects
                                }
                                var firstHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);
                                var firstHalfAbbreviation = firstHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(firstHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr1) ? abbr1 : "Unknown"
                                    : abbreviation;
                                var secondHalfAbbreviation = secondHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(secondHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr2) ? abbr2 : "Unknown"
                                    : abbreviation;
                                if (!dailyAttendance.Any())
                                {
                                    if (firstHalfLeave != null && secondHalfLeave != null)
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5{secondHalfAbbreviation}";
                                        dailyDetails.FirstCheckin = null;
                                        dailyDetails.LastCheckout = null;
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                        dailyDetails.FirstCheckin = null;
                                        dailyDetails.LastCheckout = null;
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                    {
                                        dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                        dailyDetails.FirstCheckin = null;
                                        dailyDetails.LastCheckout = null;
                                    }
                                    else
                                    {
                                        dailyDetails.Status = abbreviation;
                                        dailyDetails.FirstCheckin = firstCheckin;
                                        dailyDetails.LastCheckout = lastout_new;
                                    }
                                }
                                else
                                {
                                    if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                    {
                                        dailyDetails.Status = abbreviation; // Full day leave
                                        dailyDetails.FirstCheckin = firstCheckin;
                                        dailyDetails.LastCheckout = lastout_new;
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                    {
                                        if (totalMinutes + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                        else if (totalMinutes + halfBreak + permissionMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                            dailyDetails.FirstCheckin = null;
                                            dailyDetails.LastCheckout = null;
                                        }
                                    }
                                    else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                    {
                                        if (totalMinutes + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                        else if (totalMinutes + halfBreak + permissionMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = null;
                                            dailyDetails.LastCheckout = null;
                                        }
                                    }
                                    else if (firstHalfLeave != null && secondHalfLeave != null)
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5{secondHalfAbbreviation}";// No attendance record for the both halfves
                                        dailyDetails.FirstCheckin = null;
                                        dailyDetails.LastCheckout = null;
                                    }
                                }
                            }
                        }
                        else if (approvedLeave.Count() > 0 && wfh > 0 && dailyAttendances.Any())
                        {
                            var dailyAttendance = attendanceDetails
                               .Where(a => a.Eventtime.Date == input.date.Date)
                               .Select(a => new
                               {
                                   Eventtime = a.Eventtime.AddSeconds(-a.Eventtime.Second).AddMilliseconds(-a.Eventtime.Millisecond)
                               })
                               .ToList();
                            var firstCheckin = dailyAttendances.FirstOrDefault(a => a.Ischeckin == true)?.Eventtime;
                            var lastout_new = dailyAttendances.LastOrDefault(a => a.Ischeckin == false)?.Eventtime ?? DateTime.MinValue;
                            double totalMinutes = 0;
                            // Ensure there is both a valid check-in and check-out time
                            if (firstCheckin != null && lastout_new != DateTime.MinValue)
                            {
                                var timeDifference = lastout_new - firstCheckin.Value;
                                // If the time difference is negative, reset totalMinutes to 0
                                if (timeDifference.TotalMinutes > 0)
                                {
                                    totalMinutes = timeDifference.TotalMinutes;
                                }
                            }
                            // If totalMinutes is negative or no valid checkout, make it zero.
                            totalMinutes = totalMinutes >= 0 ? totalMinutes : 0;
                            foreach (var approvedLeaves in approvedLeave)
                            {
                                var leaveTypeId = approvedLeaves.Leave.LeaveTypeId.GetValueOrDefault();
                                var abbreviation = leaveTypeAbbreviations.TryGetValue(leaveTypeId, out var abbr) ? abbr : "Unknown";
                                var firstHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);
                                var firstHalfAbbreviation = firstHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(firstHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr1) ? abbr1 : "Unknown"
                                    : abbreviation;
                                var secondHalfAbbreviation = secondHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(secondHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr2) ? abbr2 : "Unknown"
                                    : abbreviation;
                                var firstHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh;
                                var secondHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh - firstHalfWfh;
                                if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                {
                                    dailyDetails.Status = abbreviation;
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                {
                                    if (wfh + halfBreak + totalMinutes >= halfDayThreshold)
                                    {
                                        if (secondHalfWfh + halfBreak + totalMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                        dailyDetails.FirstCheckin = firstCheckin;
                                        dailyDetails.LastCheckout = lastout_new;
                                    }
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    if (wfh + halfBreak + totalMinutes >= halfDayThreshold)
                                    {
                                        if (firstHalfWfh + halfBreak + totalMinutes >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = firstCheckin;
                                            dailyDetails.LastCheckout = lastout_new;
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                        dailyDetails.FirstCheckin = firstCheckin;
                                        dailyDetails.LastCheckout = lastout_new;
                                    }
                                }
                            }
                        }
                        else if (approvedLeave.Count() > 0 && wfh > 0)
                        {
                            foreach (var approvedLeaves in approvedLeave)
                            {
                                var leaveTypeId = approvedLeaves.Leave.LeaveTypeId.GetValueOrDefault();
                                var abbreviation = leaveTypeAbbreviations.TryGetValue(leaveTypeId, out var abbr) ? abbr : "Unknown";
                                var firstHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf);
                                var secondHalfLeave = approvedLeave.FirstOrDefault(l => l.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf);
                                var firstHalfAbbreviation = firstHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(firstHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr1) ? abbr1 : "Unknown"
                                    : abbreviation;
                                var secondHalfAbbreviation = secondHalfLeave != null
                                    ? leaveTypeAbbreviations.TryGetValue(secondHalfLeave.Leave.LeaveTypeId.GetValueOrDefault(), out var abbr2) ? abbr2 : "Unknown"
                                    : abbreviation;
                                var firstHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh;
                                var secondHalfWfh = wfh >= wfhThreshold ? wfhThreshold : wfh - firstHalfWfh;
                                if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.FullDay)
                                {
                                    dailyDetails.Status = abbreviation;
                                    dailyDetails.FirstCheckin = null;
                                    dailyDetails.LastCheckout = null;
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDayFirtstHalf)
                                {
                                    if (wfh + halfBreak >= halfDayThreshold)
                                    {
                                        if (secondHalfWfh + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5P(WFH)";
                                            dailyDetails.FirstCheckin = null;
                                            dailyDetails.LastCheckout = null;
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                            dailyDetails.FirstCheckin = null;
                                            dailyDetails.LastCheckout = null;
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5{firstHalfAbbreviation}/0.5A";
                                        dailyDetails.FirstCheckin = null;
                                        dailyDetails.LastCheckout = null;
                                    }
                                }
                                else if (approvedLeaves.LeaveDetail.TimeSlot == CustomEnum.TimeSlot.HalfDaySecondHalf)
                                {
                                    if (wfh + halfBreak >= halfDayThreshold)
                                    {
                                        if (firstHalfWfh + halfBreak >= halfDayThreshold)
                                        {
                                            dailyDetails.Status = $"0.5P(WFH)/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = null;
                                            dailyDetails.LastCheckout = null;
                                        }
                                        else
                                        {
                                            dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                            dailyDetails.FirstCheckin = null;
                                            dailyDetails.LastCheckout = null;
                                        }
                                    }
                                    else
                                    {
                                        dailyDetails.Status = $"0.5A/0.5{secondHalfAbbreviation}";
                                        dailyDetails.FirstCheckin = null;
                                        dailyDetails.LastCheckout = null;
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Handle the case where there is no leave ,attendance is used to determine status
                            var dailyAttendance = attendanceDetails
                                .Where(a => a.Eventtime.Date == input.date.Date)
                                .Select(a => new
                                {
                                    Eventtime = a.Eventtime.AddSeconds(-a.Eventtime.Second).AddMilliseconds(-a.Eventtime.Millisecond)
                                })
                                .ToList();
                            var firstCheckin = dailyAttendances.FirstOrDefault(a => a.Ischeckin == true)?.Eventtime;
                            var lastout_new = dailyAttendances.LastOrDefault(a => a.Ischeckin == false)?.Eventtime ?? DateTime.MinValue;
                            double totalMinutes = 0;
                            // Ensure there is both a valid check-in and check-out time
                            if (firstCheckin != null && lastout_new != DateTime.MinValue)
                            {
                                var timeDifference = lastout_new - firstCheckin.Value;
                                // If the time difference is negative, reset totalMinutes to 0
                                if (timeDifference.TotalMinutes > 0)
                                {
                                    totalMinutes = timeDifference.TotalMinutes;
                                }
                            }
                            // If totalMinutes is negative or no valid checkout, make it zero.
                            totalMinutes = totalMinutes >= 0 ? totalMinutes : 0;
                            if (wfh > 0 && !dailyAttendance.Any())
                            {
                                if (wfh >= totalPayableHours)
                                {
                                    dailyDetails.Status = "Present(WFH)";
                                    dailyDetails.FirstCheckin = null;
                                    dailyDetails.LastCheckout = null;
                                }
                                else if (wfh >= wfhThreshold)
                                {
                                    dailyDetails.Status = "0.5P(WFH)/0.5A";
                                    dailyDetails.FirstCheckin = null;
                                    dailyDetails.LastCheckout = null;
                                }
                                else
                                {
                                    dailyDetails.Status = "Absent";
                                    dailyDetails.FirstCheckin = null;
                                    dailyDetails.LastCheckout = null;
                                }
                            }
                            else if (wfh > 0 && dailyAttendance.Any() && permissionMinutes > 0)
                            {
                                if (totalMinutes + wfh + permissionMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "Present";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else if (totalMinutes + wfh + permissionMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                            }
                            else if (wfh > 0 && dailyAttendance.Any())
                            {
                                if (totalMinutes + wfh >= totalPayableHours)
                                {
                                    dailyDetails.Status = "Present";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else if (totalMinutes + wfh >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                            }
                            else if (wfh > 0 && permissionMinutes > 0)
                            {
                                if (wfh + permissionMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "Present";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else if (wfh + permissionMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                            }
                            else if (dailyAttendance.Any() && permissionMinutes > 0)
                            {
                                if (totalMinutes + permissionMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "Present";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else if (totalMinutes + permissionMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A";
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                            }
                            else if (dailyAttendance.Any())
                            {
                                if (totalMinutes >= totalPayableHours)
                                {
                                    dailyDetails.Status = "Present"; // Full day present without WFH
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else if (totalMinutes >= halfDayThreshold)
                                {
                                    dailyDetails.Status = "0.5P/0.5A"; // Half day present, half day absent
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                                else
                                {
                                    dailyDetails.Status = "Absent"; // Absent
                                    dailyDetails.FirstCheckin = firstCheckin;
                                    dailyDetails.LastCheckout = lastout_new;
                                }
                            }
                            else
                            {
                                dailyDetails.Status = "Absent"; // Absent
                                dailyDetails.FirstCheckin = firstCheckin;
                                dailyDetails.LastCheckout = lastout_new;
                            }
                        }
                    }
                    else if (input.date.Date > today)
                    {
                        dailyDetails.Status = "-";
                        dailyDetails.FirstCheckin = null;
                        dailyDetails.LastCheckout = null;
                    }
                }
                userReport.AttendanceDetails.Add(dailyDetails);
                userReports.Add(userReport);
            }
            return userReports;
        }

        [HttpGet]
        public async Task<AttendanceDailyCount> GetTotalAttendanceSummary(DateTime date, int tenantId)
        {
            // Call the method to generate daily attendance reports for the specified date
            List<UserAttendanceDailyReport> dailyReports = await GenerateDailyAttendanceReport(new GetAttendanceDailyReportInput { date = date, TenantId = tenantId });

            // Initialize counters for the attendance summary
            int totalPresent = 0;
            int totalWFH = 0;
            int totalAbsent = 0;
            int totalPaidLeave = 0;
            int totalUnpaidLeave = 0;
            int totalWeekend = 0;
            int totalHoliday = 0;
            int others = 0;
            HashSet<string> uniqueUsers = new HashSet<string>();
            Dictionary<string, string> userStatus = new Dictionary<string, string>();

            // Iterate over the daily reports to calculate counts
            foreach (var report in dailyReports)
            {
                if (report.AttendanceDetails != null && report.AttendanceDetails.Any())
                {
                    foreach (var detail in report.AttendanceDetails)
                    {
                        string employeeId = report.EmployeeId;
                        string status = detail.Status.Trim(); // Trim any extra spaces for better accuracy

                        // Ensure each user is only counted once by tracking the most important status
                        if (!userStatus.ContainsKey(employeeId))
                        {
                            userStatus[employeeId] = status;
                        }
                        else
                        {
                            // If a more important status comes in, update the user's status
                            userStatus[employeeId] = GetHigherPriorityStatus(userStatus[employeeId], status);
                        }
                    }
                }
            }

            // Now process the final status for each user
            foreach (var status in userStatus.Values)
            {
                if (status == "Present")
                {
                    totalPresent++;
                }
                else if (status == "Present(WFH)" || status == "WFH") // Ensure both WFH variations are counted
                {
                    totalWFH++;
                }
                else if (status == "Absent")
                {
                    totalAbsent++;
                }
                else if (IsPaidLeave(status))
                {
                    totalPaidLeave++;
                }
                else if (status == "LOP") // Unpaid leave (Loss of Pay)
                {
                    totalUnpaidLeave++;
                }
                else if (IsWeekend(date))
                {
                    totalWeekend++;
                }
                else if (IsHoliday(date))
                {
                    totalHoliday++;
                }
                // Classify anything with "0.5"
                else if (status.Contains("0.5"))
                {
                    others++;
                }
                else
                {
                    // Anything unclassified goes to "Others"
                    others++;
                }
            }

            // Return a consistent summary object with correct naming and values
            var summary = new AttendanceDailyCount
            {
                TotalPresent = totalPresent,
                TotalWFH = totalWFH,
                TotalAbsent = totalAbsent,
                TotalPaidLeave = totalPaidLeave,
                TotalUnpaidLeave = totalUnpaidLeave,
                TotalWeekend = totalWeekend,
                TotalHoliday = totalHoliday,
                Others = others,
                TotalUsers = userStatus.Count,
                Date = date
            };

            return summary;
        }

        // Helper method to check if the status is considered "Paid Leave"
        private bool IsPaidLeave(string status)
        {
            // Consolidate all statuses that should be treated as paid leave
            return status == "CL" || status == "SL" || status == "OPH";
        }

        // Helper method to prioritize attendance status
        private string GetHigherPriorityStatus(string existingStatus, string newStatus)
        {
            // Priority order: Present > WFH > Absent > PaidLeave > Others
            var statusPriority = new Dictionary<string, int>
    {
        { "Present", 1 },
        { "Present(WFH)", 2 },
        { "WFH", 2 },
        { "Absent", 3 },
        { "CL", 4 }, { "SL", 4 }, { "OPH", 4 }, // Paid leave types
        { "LOP", 5 }, // Unpaid leave
        { "0.5", 6 },
        { "Others", 7 }
    };

            int existingPriority = statusPriority.ContainsKey(existingStatus) ? statusPriority[existingStatus] : 7;
            int newPriority = statusPriority.ContainsKey(newStatus) ? statusPriority[newStatus] : 7;

            // Return the status with higher priority (lower value means higher priority)
            return newPriority < existingPriority ? newStatus : existingStatus;
        }


        [HttpGet]
        public async Task<AttendanceCountResponse> GetAttendanceCount(DateTime date, int tenantId)
        {
            // Fetch all active users for the tenant
            var activeUsers = await (from user in _userDetailRepository.GetAll()
                                     join lookupUser in _lookup_userRepository.GetAll() on user.UserId equals lookupUser.Id
                                     where user.TenantId == tenantId && lookupUser.IsActive
                                     select user.UserId).ToListAsync();



            // Fetch attendance records for the specified date and tenant
            var attendanceDetails = await _attendanceRepository.GetAll()
         .Where(e => e.Eventtime.Date == date.Date && e.TenantId == tenantId)
         .OrderBy(e => e.Eventtime)
         .Select(e => new
         {
             e.UserId, // User ID from attendance record
             Eventtime = e.Eventtime, // Event timestamp
             IsCheckin = e.Ischeckin // Boolean indicating check-in or check-out
         })
         .ToListAsync();



            // Group attendance by UserId and select only the last event for each user (most recent event per day)
            var lastEventByUser = attendanceDetails
         .GroupBy(e => e.UserId)
         .Select(g => g.OrderByDescending(e => e.Eventtime).FirstOrDefault())
         .ToList();



            // Initialize counts
            int totalInCount = 0;
            int totalOutCount = 0;



            // Evaluate the final event per user to determine if they're counted as "In" or "Out"
            foreach (var eventRecord in lastEventByUser)
            {
                if (eventRecord.IsCheckin == true)
                {
                    totalInCount++; // User's last event is a check-in, count as "In"
                }
                else if (eventRecord.IsCheckin == false)
                {
                    totalOutCount++; // User's last event is a check-out, count as "Out"
                }
            }



            // Calculate YTC (Yet To Check In) for users who have no attendance records for the day
            int ytcCount = activeUsers.Count(userId => !lastEventByUser.Any(e => e.UserId == userId));



            // Prepare the response
            var response = new AttendanceCountResponse
            {
                TotalIn = totalInCount,
                TotalOut = totalOutCount,
                YTC = ytcCount
            };



            return response;
        }



    }




}