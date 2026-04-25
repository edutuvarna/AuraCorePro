import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

export interface PermissionRequest {
  id: string;
  adminEmail: string;
  permissionKey: string;
  reason: string;
  requestedAt: string;
}

export function usePermissionRequests() {
  return useQuery<{ items: PermissionRequest[] }>({
    queryKey: ['permissionRequests'],
    queryFn: () => api.getPermissionRequests(),
    refetchOnMount: 'always',
  });
}

export function useApproveRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, expiresAt }: { id: string; expiresAt?: string }) =>
      api.approvePermissionRequest(id, expiresAt),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['permissionRequests'] }),
  });
}

export function useDenyRequest() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, reviewNote }: { id: string; reviewNote: string }) =>
      api.denyPermissionRequest(id, reviewNote),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['permissionRequests'] }),
  });
}
